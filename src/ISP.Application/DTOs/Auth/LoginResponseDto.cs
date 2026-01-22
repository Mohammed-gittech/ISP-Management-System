// ============================================
// Auth DTOs
// ============================================

namespace ISP.Application.DTOs.Auth
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }

    }
}