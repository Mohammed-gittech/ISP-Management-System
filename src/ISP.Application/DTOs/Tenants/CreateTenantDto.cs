// ============================================
// Tenant DTOs
// ============================================
using ISP.Domain.Enums;

namespace ISP.Application.DTOs.Tenants
{
    public class CreateTenantDto
    {
        public string Name { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }

        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }

        public TenantPlan SubscriptionPlan { get; set; } = TenantPlan.Free;
        public String? TelegramBotToken { get; set; }


        // Admin User Details
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }
}