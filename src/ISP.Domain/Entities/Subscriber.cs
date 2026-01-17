// ============================================
// Subscriber.cs - المشترك (العميل النهائي)
// ============================================

using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class Subscriber : BaseEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }

        public string? TelegramChatId { get; set; }
        public string? TelegramUsername { get; set; }

        public string? NationalId { get; set; }

        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public SubscriberStatus Status { get; set; } = SubscriberStatus.Active;

        public string? Notes { get; set; }

        // Navigation Properties
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    }
}