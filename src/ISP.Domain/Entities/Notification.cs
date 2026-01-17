// ============================================
// Notification.cs - الإشعارات
// ============================================
using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class Notification : BaseEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public int SubscriberId { get; set; }
        public Subscriber Subscriber { get; set; } = null!;

        public NotificationType Type { get; set; }
        public string Message { get; set; } = string.Empty;

        public NotificationChannel Channel { get; set; } = NotificationChannel.Telegram;

        public DateTime? SentDat { get; set; }
        public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

        public string? ErrorMessage { get; set; }
        public long? TelegramMessageId { get; set; }
    }
}