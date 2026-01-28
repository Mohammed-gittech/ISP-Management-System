// ============================================
// UserDto.cs - عرض بيانات المستخدم
// ============================================
namespace ISP.Application.DTOs.Users
{
    public class UserDto
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}