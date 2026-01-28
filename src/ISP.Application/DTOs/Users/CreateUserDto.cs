// ============================================
// CreateUserDto.cs - إنشاء مستخدم جديد
// ============================================
namespace ISP.Application.DTOs.Users
{
    public class CreateUserDto
    {
        public int? TenantId { get; set; } // null = SuperAdmin
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee"; // SuperAdmin, TenantAdmin, Employee
    }
}