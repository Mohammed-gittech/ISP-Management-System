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
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantDto dto)
        {
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
    }
}