
namespace ISP.Application.DTOs.Subscribers
{
    public class SubscriberDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TelegramUsername { get; set; }
        public bool HasTelegram { get; set; }
        public string? NationalId { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // Current Subscription Info
        public SubscriptionDto? CurrentSubscription { get; set; }
    }
}