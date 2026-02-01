using System.Security.Claims;
using ISP.Application.DTOs.Users;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة المستخدمين
    /// ✅ Soft Delete Support
    /// ✅ Extra Security Checks
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ============================================
        // BASIC CRUD
        // ============================================

        /// <summary>
        /// الحصول على كل المستخدمين (النشطين فقط)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var result = await _userService.GetAllAsync(page, pageSize, search);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// الحصول على مستخدم بالـ Id
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = "المستخدم غير موجود" });

            // TenantAdmin يرى مستخدمي وكيله فقط
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (user.TenantId != currentTenantId)
                    return Forbid();
            }

            return Ok(new { success = true, data = user });
        }

        /// <summary>
        /// الحصول على مستخدمي Tenant معين
        /// </summary>
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetByTenant(
            int tenantId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // TenantAdmin يرى وكيله فقط
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (tenantId != currentTenantId)
                    return Forbid();
            }

            var result = await _userService.GetUsersByTenantAsync(tenantId, page, pageSize);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// إنشاء مستخدم جديد
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                // TenantAdmin يُنشئ مستخدمين لوكيله فقط
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (currentUserRole == "TenantAdmin")
                {
                    var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                    if (dto.TenantId != currentTenantId)
                        return Forbid();

                    // TenantAdmin لا يمكنه إنشاء SuperAdmin
                    if (dto.Role == "SuperAdmin")
                        return Forbid();
                }

                var user = await _userService.CreateAsync(dto);
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = user.Id },
                    new { success = true, message = "تم إنشاء المستخدم بنجاح", data = user });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// تحديث مستخدم
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                // TenantAdmin يعدل مستخدمي وكيله فقط
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (currentUserRole == "TenantAdmin")
                {
                    var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                    if (user.TenantId != currentTenantId)
                        return Forbid();
                }

                var updated = await _userService.UpdateAsync(id, dto);
                return Ok(new { success = true, message = "تم تحديث المستخدم بنجاح", data = updated });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// حذف ناعم لمستخدم
        /// ⚠️ منع حذف النفس
        /// ⚠️ منع حذف آخر SuperAdmin
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var user = await _userService.GetByIdAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                // TenantAdmin يحذف مستخدمي وكيله فقط
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (currentUserRole == "TenantAdmin")
                {
                    var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                    if (user.TenantId != currentTenantId)
                        return Forbid();

                    // TenantAdmin لا يمكنه حذف SuperAdmin
                    if (user.Role == "SuperAdmin")
                        return Forbid();
                }

                var deleted = await _userService.DeleteAsync(id);

                if (!deleted)
                    return NotFound(new { success = false, message = "فشل الحذف" });

                return Ok(new
                {
                    success = true,
                    message = "تم حذف المستخدم بنجاح (يمكن استرجاعه لاحقاً)"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// الحصول على المستخدمين المحذوفين
        /// </summary>
        [HttpGet("deleted")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetDeleted(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _userService.GetDeletedAsync(page, pageSize);

            return Ok(new
            {
                success = true,
                message = $"تم العثور على {result.TotalCount} مستخدم محذوف",
                data = result
            });
        }

        /// <summary>
        /// استرجاع مستخدم محذوف
        /// ✅ يتحقق من عدم تكرار Email/Username قبل الاسترجاع
        /// </summary>
        [HttpPost("{id}/restore")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                var restored = await _userService.RestoreAsync(id);

                if (!restored)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "المستخدم غير موجود أو غير محذوف"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "تم استرجاع المستخدم بنجاح"
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
        /// حذف نهائي لمستخدم
        /// ⚠️ SuperAdmin only
        /// ⚠️ لا يمكن التراجع
        /// ⚠️ لا يمكن حذف SuperAdmin نهائياً
        /// </summary>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            try
            {
                var deleted = await _userService.PermanentDeleteAsync(id);

                if (!deleted)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "المستخدم غير موجود"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "⚠️ تم الحذف النهائي للمستخدم - لا يمكن الاسترجاع"
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
        // PASSWORD OPERATIONS
        // ============================================

        /// <summary>
        /// تغيير كلمة المرور (المستخدم الحالي)
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _userService.ChangePasswordAsync(currentUserId, dto);

                if (!result)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = "تم تغيير كلمة المرور بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// إعادة تعيين كلمة المرور (Admin)
        /// </summary>
        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = "المستخدم غير موجود" });

            // TenantAdmin يعيد تعيين كلمات المرور لوكيله فقط
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (currentUserRole == "TenantAdmin")
            {
                var currentTenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0");
                if (user.TenantId != currentTenantId)
                    return Forbid();
            }

            var result = await _userService.ResetPasswordAsync(id, dto.NewPassword);

            if (!result)
                return NotFound(new { success = false, message = "فشل إعادة التعيين" });

            return Ok(new { success = true, message = "تم إعادة تعيين كلمة المرور بنجاح" });
        }

        /// <summary>
        /// تعيين دور لمستخدم
        /// </summary>
        [HttpPost("{id}/assign-role")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> AssignRole(int id, [FromBody] AssignRoleDto dto)
        {
            try
            {
                var result = await _userService.AssignRoleAsync(id, dto.Role);

                if (!result)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = $"تم تعيين الدور {dto.Role} بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}