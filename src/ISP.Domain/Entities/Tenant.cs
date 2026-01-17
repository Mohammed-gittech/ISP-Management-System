// ============================================
// Tenant.cs - الوكيل (ISP Company)
// ============================================
using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class Tenant : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Subdomain { get; set; }
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }

        public TenantPlan SubscriptionPlan { get; set; } = TenantPlan.Free;
        public int MaxSubscribers { get; set; } = 50;// Default للـ Free Plan

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpirationDate { get; set; }

        public string? TelegramBotToken { get; set; }

        // Navigation Properties
        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<TenantSubscription> TenantSubscriptions { get; set; } = new List<TenantSubscription>();
        public ICollection<Subscriber> Subscribers { get; set; } = new List<Subscriber>();
        public ICollection<Plan> Plans { get; set; } = new List<Plan>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();


    }
}