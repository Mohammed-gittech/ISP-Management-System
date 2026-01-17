// ============================================
// Plan.cs - باقة الإنترنت
// ============================================

namespace ISP.Domain.Entities
{
    public class Plan : BaseEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public int Speed { get; set; } // Speed in Mbps
        public decimal Price { get; set; } // Price per month
        public int DurationDays { get; set; } = 30; // Duration in days

        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}