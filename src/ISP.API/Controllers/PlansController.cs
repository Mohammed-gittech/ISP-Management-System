using Azure;
using ISP.Application.DTOs.Plans;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الباقات
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

        /// <summary>
        /// الحصول على كل الباقات
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int Page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _service.GetAllAsync(Page, pageSize);

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
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePlanDto dto)
        {
            await _service.UpdateAsync(id, dto);

            return Ok(new
            {
                success = true,
                message = "تم تحديث الباقة بنجاح"
            });
        }

        /// <summary>
        /// تعطيل باقة
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
        /// تفعيل باقة
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