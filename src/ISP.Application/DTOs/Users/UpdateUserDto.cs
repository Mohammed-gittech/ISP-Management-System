// ============================================
// UpdateUserDto.cs - تعديل بيانات المستخدم
// ============================================
namespace ISP.Application.DTOs.Users
{
    public class UpdateUserDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public bool? IsActive { get; set; }
    }
}