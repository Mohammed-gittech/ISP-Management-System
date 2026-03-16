using ISP.Application.DTOs.Tenants;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الوكلاء
    /// ✅ Resource-Based Authorization via BaseController
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TenantsController : BaseController
    {
        private readonly ITenantService _service;

        public TenantsController(
            ITenantService service,
            IAuthorizationService authorizationService)
            : base(authorizationService)
        {
            _service = service;
        }

        /// <summary>
        /// إنشاء وكيل جديد (مع Admin User)
        /// لا يحتاج Authorization - للتسجيل
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            // TenantAdmin يرى وكيله فقط
            if (IsCrossTenantAccess(id))
                return Forbid();

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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            // TenantAdmin يعدل وكيله فقط
            if (IsCrossTenantAccess(id))
                return Forbid();

            try
            {
                await _service.UpdateAsync(id, dto);

                return Ok(new { success = true, message = "تم تحديث البيانات بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// تعطيل حساب وكيل (SuperAdmin فقط)
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubscribersCount(int id)
        {
            // TenantAdmin يرى وكيله فقط
            if (IsCrossTenantAccess(id))
                return Forbid();

            var tenant = await _service.GetByIdAsync(id);

            if (tenant == null)
                return NotFound(new { success = false, message = "الوكيل غير موجود" });

            var count = await _service.GetCurrentSubscribersCountAsync(id);

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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RenewRequest(int id, [FromBody] RenewTenantSubscriptionDto dto)
        {
            // TenantAdmin يجدد اشتراكه فقط
            if (IsCrossTenantAccess(id))
                return Forbid();

            try
            {
                var result = await _service.RenewRequestAsync(id, dto);

                return Ok(new
                {
                    success = true,
                    message = "تم إرسال طلب التجديد بنجاح — سيتم التواصل معك بعد تأكيد الدفع",
                    data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// تأكيد استلام الدفع وتفعيل الوكيل — SuperAdmin فقط
        /// </summary>
        [HttpPost("{id}/confirm-payment")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ConfirmPayment(int id, [FromBody] ConfirmTenantPaymentDto dto)
        {
            try
            {
                await _service.ConfirmPaymentAsync(id, dto);

                return Ok(new { success = true, message = "تم تأكيد الدفع وتفعيل الحساب بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// عرض كل الطلبات المعلقة — SuperAdmin فقط
        /// لمعرفة من يحتاج تأكيد دفع
        /// </summary>
        [HttpGet("pending-renewals")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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