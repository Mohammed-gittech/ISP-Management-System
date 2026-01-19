// ============================================
// TenantSubscription.cs - اشتراك الوكيل
// ============================================
using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class TenantSubscription : BaseEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public TenantPlan Plan { get; set; }
        public decimal Price { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public TenantSubscriptionStatus Status { get; set; } = TenantSubscriptionStatus.Active;

        public string? PaymentMethod { get; set; }
        public DateTime? LastPaymentDate { get; set; }

    }
}