// ============================================
// Tenant DTOs
// ============================================

namespace ISP.Application.DTOs.Tenants
{
    public class UpdateTenantDto
    {
        public string? Name { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? TelegramBotToken { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
    }
}