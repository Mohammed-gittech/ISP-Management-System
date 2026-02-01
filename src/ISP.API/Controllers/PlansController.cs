using ISP.Application.DTOs.Plans;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الباقات
    /// ✅ Multi-Tenancy: Repository Filter يطبق تلقائياً
    /// ✅ Soft Delete Support
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PlansController : ControllerBase
    {
        private readonly IPlanService _service;

        public PlansController(IPlanService service)
        {
            _service = service;
        }

        // ============================================
        // BASIC CRUD OPERATIONS
        // ============================================

        /// <summary>
        /// الحصول على كل الباقات
        /// ✅ Repository Filter: يرجع باقات Tenant الحالي فقط (النشطة)
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
        /// الحصول على الباقات النشطة فقط
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var result = await _service.GetActiveAsync();

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// الحصول على باقة بالـ Id
        /// ✅ Repository Filter: إذا كانت من Tenant آخر أو محذوفة يرجع null
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
                    message = $"الباقة برقم {id} غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// إنشاء باقة جديدة
        /// ✅ Service: يعين TenantId تلقائياً
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePlanDto dto)
        {
            var result = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                new
                {
                    success = true,
                    message = "تم إنشاء الباقة بنجاح",
                    data = result
                }
            );
        }

        /// <summary>
        /// تحديث باقة
        /// ✅ Repository Filter: GetByIdAsync يتحقق من Ownership
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePlanDto dto)
        {
            try
            {
                await _service.UpdateAsync(id, dto);

                return Ok(new
                {
                    success = true,
                    message = "تم تحديث الباقة بنجاح"
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
        /// حذف ناعم للباقة
        /// ⚠️ لا يمكن حذف باقة لها اشتراكات نشطة
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _service.DeleteAsync(id);

                if (!deleted)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "الباقة غير موجودة"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "تم حذف الباقة بنجاح (يمكن الاسترجاع)"
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
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// استرجاع باقة محذوفة
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
                    message = "الباقة غير موجودة أو غير محذوفة"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم استرجاع الباقة بنجاح"
            });
        }

        /// <summary>
        /// الحصول على الباقات المحذوفة
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
                message = "الباقات المحذوفة (يمكن استرجاعها)"
            });
        }

        /// <summary>
        /// حذف نهائي (SuperAdmin فقط)
        /// ⚠️ لا يمكن الحذف إذا كانت مستخدمة في اشتراكات
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
                        message = "الباقة غير موجودة"
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
        // ACTIVATE/DEACTIVATE (موجود مسبقاً)
        // ============================================

        /// <summary>
        /// تعطيل باقة (IsActive = false)
        /// ℹ️ مختلف عن Soft Delete
        /// </summary>
        [HttpPost("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result = await _service.DeactivateAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الباقة غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تعطيل الباقة بنجاح"
            });
        }

        /// <summary>
        /// تفعيل باقة (IsActive = true)
        /// </summary>
        [HttpPost("{id}/activate")]
        public async Task<IActionResult> Activate(int id)
        {
            var result = await _service.ActivateAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الباقة غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تفعيل الباقة بنجاح"
            });
        }
    }
}