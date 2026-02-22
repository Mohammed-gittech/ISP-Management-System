using System.Security.Claims;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الوكلاء
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _service;

        public TenantsController(ITenantService service)
        {
            _service = service;
        }

        /// <summary>
        /// إنشاء وكيل جديد (مع Admin User)
        /// لا يحتاج Authorization - للتسجيل
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] CreateTenantDto dto)
        {
            var result = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                new
                {
                    success = true,
                    message = "تم إنشاء الحساب بنجاح. يمكنك الآن تسجيل الدخول",
                    data = result
                });
        }

        /// <summary>
        /// الحصول على كل الوكلاء (SuperAdmin فقط)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
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
        /// الحصول على وكيل بالـ Id
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(int id)
        {
            // ✅ Ownership Check: TenantAdmin يرى وكيله فقط
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (id != currentTenantId)
                    return Forbid();
            }

            var result = await _service.GetByIdAsync(id);

            if (result == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"الوكيل برقم {id} غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        /// <summary>
        /// تحديث بيانات وكيل
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            // ✅ Ownership Check: TenantAdmin يعدل وكيله فقط
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (id != currentTenantId)
                    return Forbid();
            }
            await _service.UpdateAsync(id, dto);

            return Ok(new
            {
                success = true,
                message = "تم تحديث البيانات بنجاح"
            });
        }

        /// <summary>
        /// تعطيل حساب وكيل (SuperAdmin فقط)
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Deactivate(int id)
        {
            var result = await _service.DeactivateAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الوكيل غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تعطيل الحساب بنجاح"
            });
        }

        /// <summary>
        /// تفعيل حساب وكيل (SuperAdmin فقط)
        /// </summary>
        [HttpPost("{id}/activate")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Activate(int id)
        {
            var result = await _service.ActivateAsync(id);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الوكيل غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تفعيل الحساب بنجاح"
            });
        }

        /// <summary>
        /// عدد المشتركين الحاليين للوكيل
        /// </summary>
        [HttpGet("{id}/subscribers-count")]
        [Authorize]
        public async Task<IActionResult> GetSubscribersCount(int id)
        {
            // ✅ Ownership Check
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (id != currentTenantId)
                    return Forbid();
            }

            var count = await _service.GetCurrentSubscribersCountAsync(id);
            var tenant = await _service.GetByIdAsync(id);

            if (tenant == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "الوكيل غير موجود"
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    tenantId = id,
                    tenantName = tenant.Name,
                    currentSubscribers = count,
                    maxSubscribers = tenant.MaxSubscribers,
                    canAddMore = count < tenant.MaxSubscribers,
                    remaining = tenant.MaxSubscribers - count
                }
            });
        }

        /// <summary>
        /// طلب تجديد اشتراك الوكيل — TenantAdmin فقط
        /// ينشئ طلب معلق ينتظر تأكيد SuperAdmin
        /// </summary>
        [HttpPost("{id}/renew-request")]
        [Authorize(Roles = "TenantAdmin")]
        public async Task<IActionResult> RenewRequest(int id, [FromBody] RenewTenantSubscriptionDto dto)
        {
            // Ownership Check: TenantAdmin يجدد اشتراكه فقط
            var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
            if (id != currentTenantId)
                return Forbid();

            var result = await _service.RenewRequestAsync(id, dto);

            return Ok(new
            {
                success = true,
                message = "تم إرسال طلب التجديد بنجاح — سيتم التواصل معك بعد تأكيد الدفع",
                data = result
            });
        }

        /// <summary>
        /// تأكيد استلام الدفع وتفعيل الوكيل — SuperAdmin فقط
        /// </summary>
        [HttpPost("{id}/confirm-payment")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ConfirmPayment(int id, [FromBody] ConfirmTenantPaymentDto dto)
        {
            await _service.ConfirmPaymentAsync(id, dto);

            return Ok(new
            {
                success = true,
                message = "تم تأكيد الدفع وتفعيل الحساب بنجاح"
            });
        }

        /// <summary>
        /// عرض كل الطلبات المعلقة — SuperAdmin فقط
        /// لمعرفة من يحتاج تأكيد دفع
        /// </summary>
        [HttpGet("pending-renewals")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetPendingRenewals()
        {
            var result = await _service.GetPendingRenewalsAsync();

            return Ok(new
            {
                success = true,
                data = result
            });
        }
    }
}