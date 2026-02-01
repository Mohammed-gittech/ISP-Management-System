using ISP.Application.DTOs.Subscribers;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة المشتركين
    /// ✅ Multi-Tenancy: Repository Filter يطبق تلقائياً
    /// ✅ Soft Delete Support
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SubscribersController : ControllerBase
    {
        private readonly ISubscriberService _service;

        public SubscribersController(ISubscriberService service)
        {
            _service = service;
        }

        // ============================================
        // BASIC CRUD OPERATIONS
        // ============================================

        /// <summary>
        /// الحصول على كل المشتركين (مع Pagination)
        /// ✅ Repository Filter: يرجع مشتركي Tenant الحالي فقط (النشطين)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllAsync(page, pageSize);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// الحصول على مشترك بالـ Id
        /// ✅ Repository Filter: إذا كان من Tenant آخر أو محذوف يرجع null
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"المشترك برقم {id} غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// البحث عن مشتركين
        /// ✅ Repository Filter: يبحث في مشتركي Tenant الحالي فقط (النشطين)
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (string.IsNullOrEmpty(q))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "نص البحث مطلوب"
                });
            }

            var result = await _service.SearchAsync(q, page, pageSize);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// إنشاء مشترك جديد
        /// ✅ Service: يعين TenantId تلقائياً من CurrentTenantService
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriberDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = result.Id },
                    new
                    {
                        success = true,
                        message = "تم إنشاء المشترك بنجاح",
                        data = result
                    });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// تحديث مشترك
        /// ✅ Repository Filter: GetByIdAsync يتحقق من Ownership
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSubscriberDto dto)
        {
            try
            {
                await _service.UpdateAsync(id, dto);

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث المشترك بنجاح"
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// حذف ناعم (Soft Delete) - الطريقة الموصى بها
        /// ✅ يحذف Subscriptions المرتبطة تلقائياً (Manual Cascade)
        /// ✅ يمكن استرجاع المشترك لاحقاً
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "تم حذف المشترك بنجاح (يمكن الاسترجاع)"
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// استرجاع مشترك محذوف
        /// ⚠️ لا يسترجع Subscriptions تلقائياً
        /// </summary>
        [HttpPost("{id}/restore")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Restore(int id)
        {
            var restored = await _service.RestoreAsync(id);

            if (!restored)
            {
                return NotFound(new
                {
                    success = false,
                    message = "المشترك غير موجود أو غير محذوف"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم استرجاع المشترك بنجاح"
            });
        }

        /// <summary>
        /// الحصول على المشتركين المحذوفين
        /// </summary>
        [HttpGet("deleted")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetDeleted(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetDeletedAsync(page, pageSize);

            return Ok(new
            {
                success = true,
                data = result,
                message = "المشتركون المحذوفون (يمكن استرجاعهم)"
            });
        }

        /// <summary>
        /// حذف نهائي من Database (SuperAdmin فقط)
        /// ⚠️ لا يمكن التراجع
        /// ⚠️ يحذف Subscriptions المرتبطة نهائياً
        /// </summary>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            try
            {
                var deleted = await _service.PermanentDeleteAsync(id);

                if (!deleted)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "المشترك غير موجود"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "⚠️ تم الحذف النهائي - لا يمكن الاسترجاع"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================
        // HELPER OPERATIONS
        // ============================================

        /// <summary>
        /// ربط مشترك بـ Telegram
        /// </summary>
        [HttpPost("{id}/link-telegram")]
        public async Task<IActionResult> LinkTelegram(int id, [FromBody] LinkTelegramDto dto)
        {
            var result = await _service.LinkTelegramAsync(id, dto.ChatId);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "المشترك غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم ربط Telegram بنجاح"
            });
        }
    }
}