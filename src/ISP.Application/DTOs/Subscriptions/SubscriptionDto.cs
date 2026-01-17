
namespace ISP.Application.DTOs.Subscriptions
{
    public class SubscriptionDto
    {
        public int Id { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberName { get; set; } = string.Empty;
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int Speed { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int DaysRemaining { get; set; }
        public bool AutoRenew { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}