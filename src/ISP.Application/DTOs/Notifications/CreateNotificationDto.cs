using ISP.Domain.Enums;

namespace ISP.Application.DTOs.Notifications
{
    public class CreateNotificationDto
    {
        // <summary>
        /// معرف الوكيل (صاحب المشترك)
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// معرف المشترك الذي سيستقبل الإشعار
        /// </summary>
        public int SubscriberId { get; set; }

        /// <summary>
        /// نوع الإشعار
        /// ExpiryWarning, PaymentReminder, SubscriptionRenewed, SystemAlert
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// نص الرسالة (سيتم إرساله للمشترك)
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// قناة الإرسال
        /// Default: Telegram (أساسي ومجاني)
        /// </summary>
        public NotificationChannel Channel { get; set; } = NotificationChannel.Telegram;
    }
}