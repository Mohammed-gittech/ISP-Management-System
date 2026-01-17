// ============================================
// Tenant DTOs
// ============================================

namespace ISP.Application.DTOs.Tenants
{
    public class TenantDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Subdomain { get; set; }
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string SubscriptionPlan { get; set; } = string.Empty;
        public int MaxSubscribers { get; set; }
        public int CurrentSubscribers { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool HasTelegramBot { get; set; }
    }
}