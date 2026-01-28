using System.Security.Claims;
using ISP.Application.DTOs.Users;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // يجب تسجيل الدخول
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // 1. GET ALL USERS (SuperAdmin only)
        // ============================================
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null)
        {
            var result = await _userService.GetAllAsync(page, pageSize, search);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 2. GET USER BY ID
        // ============================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            // يمكن للمستخدم رؤية بياناته فقط (إلا إذا كان Admin)
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (currentUserId != id && currentUserRole != "SuperAdmin" && currentUserRole != "TenantAdmin")
                return Forbid();

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = "المستخدم غير موجود" });

            return Ok(new { success = true, data = user });
        }

        // ============================================
        // 3. CREATE USER (Admin only)
        // ============================================
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            try
            {
                // TenantAdmin يمكنه إنشاء مستخدمين لوكيله فقط
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
                return CreatedAtAction(nameof(GetById), new { id = user.Id }, new { success = true, data = user });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 4. UPDATE USER
        // ============================================
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            try
            {
                // يمكن للمستخدم تعديل بياناته أو Admin يعدل أي مستخدم
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

                if (currentUserId != id && currentUserRole != "SuperAdmin" && currentUserRole != "TenantAdmin")
                    return Forbid();

                var user = await _userService.UpdateAsync(id, dto);
                if (user == null)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = "تم التحديث بنجاح", data = user });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 5. DELETE USER (Admin only)
        // ============================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var deleted = await _userService.DeleteAsync(id);
                if (!deleted)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = "تم الحذف بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 6. CHANGE PASSWORD (Own account)
        // ============================================
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var changed = await _userService.ChangePasswordAsync(currentUserId, dto);

                if (!changed)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = "تم تغيير كلمة المرور بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 7. RESET PASSWORD (Admin only)
        // ============================================
        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] string newPassword)
        {
            var reset = await _userService.ResetPasswordAsync(id, newPassword);
            if (!reset)
                return NotFound(new { success = false, message = "المستخدم غير موجود" });

            return Ok(new { success = true, message = "تم إعادة تعيين كلمة المرور بنجاح" });
        }

        // ============================================
        // 8. ASSIGN ROLE (SuperAdmin only)
        // ============================================
        [HttpPost("{id}/assign-role")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> AssignRole(int id, [FromBody] AssignRoleDto dto)
        {
            try
            {
                var assigned = await _userService.AssignRoleAsync(id, dto.Role);
                if (!assigned)
                    return NotFound(new { success = false, message = "المستخدم غير موجود" });

                return Ok(new { success = true, message = "تم تعيين الدور بنجاح" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ============================================
        // 9. GET USERS BY TENANT (TenantAdmin)
        // ============================================
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetByTenant(int tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // TenantAdmin يمكنه رؤية مستخدمي وكيله فقط
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
    }
}