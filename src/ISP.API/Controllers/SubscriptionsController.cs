using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الاشتراكات
    /// ✅ Multi-Tenancy: Repository Filter يطبق تلقائياً
    /// ✅ Soft Delete Support
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _service;

        public SubscriptionsController(ISubscriptionService service)
        {
            _service = service;
        }

        // ============================================
        // BASIC CRUD OPERATIONS
        // ============================================

        /// <summary>
        /// الحصول على كل الاشتراكات
        /// ✅ Repository Filter: يرجع اشتراكات Tenant الحالي فقط (النشطة)
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
        /// الحصول على اشتراك بالـ Id
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
                    message = $"الاشتراك برقم {id} غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// الحصول على الاشتراك الحالي لمشترك
        /// </summary>
        [HttpGet("subscriber/{subscriberId}/current")]
        public async Task<IActionResult> GetCurrentBySubscriber(int subscriberId)
        {
            var result = await _service.GetCurrentBySubscriberIdAsync(subscriberId);

            if (result == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "لا يوجد اشتراك نشط"
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// الحصول على الاشتراكات المنتهية قريباً
        /// </summary>
        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiring(
            [FromQuery] int days = 7,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetExpiringAsync(days, page, pageSize);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// الحصول على الاشتراكات المنتهية
        /// </summary>
        [HttpGet("expired")]
        public async Task<IActionResult> GetExpired([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetExpiredAsync(page, pageSize);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// إنشاء اشتراك جديد
        /// ✅ Service: يعين TenantId تلقائياً
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubscriptionDto dto)
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
                        message = "تم إنشاء الاشتراك بنجاح",
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
        /// تجديد اشتراك
        /// ✅ يحذف القديم (Soft Delete) ويُنشئ جديد
        /// </summary>
        [HttpPost("renew")]
        public async Task<IActionResult> Renew([FromBody] RenewSubscriptionDto dto)
        {
            try
            {
                var result = await _service.RenewAsync(dto);

                return Ok(new
                {
                    success = true,
                    message = "تم تجديد الاشتراك بنجاح",
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
        /// إلغاء اشتراك (Soft Delete)
        /// ✅ يمكن استرجاعه لاحقاً
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _service.CancelAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الاشتراك غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم إلغاء الاشتراك بنجاح (يمكن الاسترجاع)"
            });
        }

        // ============================================
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// استرجاع اشتراك ملغي
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
                    message = "الاشتراك غير موجود أو غير ملغي"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم استرجاع الاشتراك بنجاح"
            });
        }

        /// <summary>
        /// الحصول على الاشتراكات الملغاة
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
                message = "الاشتراكات الملغاة (يمكن استرجاعها)"
            });
        }

        /// <summary>
        /// حذف نهائي (SuperAdmin فقط)
        /// ⚠️ لا يمكن التراجع
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
                        message = "الاشتراك غير موجود"
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
    }
}