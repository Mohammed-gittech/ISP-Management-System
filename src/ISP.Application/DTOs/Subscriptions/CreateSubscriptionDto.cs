
namespace ISP.Application.DTOs.Subscriptions
{
    public class CreateSubscriptionDto
    {
        public int SubscriberId { get; set; }
        public int PlanId { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public bool AutoRenew { get; set; } = false;
        public string? Notes { get; set; }
    }
}