// ============================================
// AssignRoleDto.cs - تعيين دور (Role) للمستخدم
// ============================================
namespace ISP.Application.DTOs.Users
{
    public class AssignRoleDto
    {
        public string Role { get; set; } = string.Empty; // SuperAdmin, TenantAdmin, Employee
    }
}