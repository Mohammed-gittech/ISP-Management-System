// ============================================
// ChangePasswordDto.cs - تغيير كلمة المرور
// ============================================
namespace ISP.Application.DTOs.Users
{
    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}