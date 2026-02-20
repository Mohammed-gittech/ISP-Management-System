// ============================================
// Subscription.cs - اشتراك المشترك
// ============================================

using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class Subscription : BaseEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public int SubscriberId { get; set; }
        public Subscriber Subscriber { get; set; } = null!;

        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
        public bool AutoRenew { get; set; } = false;

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public void CalculateEndDate()
        {
            EndDate = StartDate.AddDays(Plan.DurationDays);
        }

        public void UpdateStatus(DateTime? referenceDate = null)
        {
            var now = referenceDate ?? DateTime.UtcNow;
            var daysRemaining = (EndDate - now).Days;

            if (daysRemaining <= 0)
            {
                Status = SubscriptionStatus.Expired;
            }
            else if (daysRemaining <= 7)
            {
                Status = SubscriptionStatus.Expiring;
            }
            else
                Status = SubscriptionStatus.Active;
        }
    }
}