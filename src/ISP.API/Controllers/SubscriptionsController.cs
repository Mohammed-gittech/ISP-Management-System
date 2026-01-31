using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الاشتراكات
    /// ✅ Multi-Tenancy: Repository Filter يطبق تلقائياً
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

        /// <summary>
        /// الحصول على كل الاشتراكات
        /// ✅ Repository Filter: يرجع اشتراكات Tenant الحالي فقط
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
        /// ✅ Repository Filter: إذا كان من Tenant آخر يرجع null
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

        /// <summary>
        /// تجديد اشتراك
        /// </summary>
        [HttpPost("renew")]
        public async Task<IActionResult> Renew([FromBody] RenewSubscriptionDto dto)
        {
            var result = await _service.RenewAsync(dto);

            return Ok(new
            {
                success = true,
                message = "تم تجديد الاشتراك بنجاح",
                data = result
            });
        }

        /// <summary>
        /// إلغاء اشتراك
        /// ✅ Repository Filter: GetByIdAsync يتحقق من Ownership
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
                message = "تم إلغاء الاشتراك بنجاح"
            });
        }
    }
}