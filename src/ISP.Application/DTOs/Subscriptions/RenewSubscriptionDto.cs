
namespace ISP.Application.DTOs.Subscriptions
{
    public class RenewSubscriptionDto
    {
        public int SubscriptionId { get; set; }
        public int? NewPlanId { get; set; } // null = نفس الباقة
        public bool AutoRenew { get; set; } = false;
        public string? Notes { get; set; }
    }
}