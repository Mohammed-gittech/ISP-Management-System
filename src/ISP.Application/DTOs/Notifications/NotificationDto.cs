using ISP.Domain.Enums;

namespace ISP.Application.DTOs.Notifications
{
    public class NotificationDto
    {
        /// <summary>
        /// معرف الإشعار
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// معرف الوكيل
        /// </summary>
        public int TenantId { get; set; }

        /// <summary>
        /// اسم الوكيل (من Tenant)
        /// </summary>
        public string TenantName { get; set; } = string.Empty;

        /// <summary>
        /// معرف المشترك
        /// </summary>
        public int SubscriberId { get; set; }

        /// <summary>
        /// اسم المشترك (من Subscriber)
        /// </summary>
        public string SubscriberName { get; set; } = string.Empty;

        /// <summary>
        /// رقم هاتف المشترك
        /// </summary>
        public string SubscriberPhone { get; set; } = string.Empty;

        /// <summary>
        /// نوع الإشعار
        /// </summary>
        public NotificationType Type { get; set; }

        /// <summary>
        /// نص الرسالة
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// قناة الإرسال
        /// </summary>
        public NotificationChannel Channel { get; set; }

        /// <summary>
        /// تاريخ الإرسال (null إذا لم يُرسل بعد)
        /// </summary>
        public DateTime? SentDate { get; set; }

        /// <summary>
        /// حالة الإشعار (Pending, Sent, Failed)
        /// </summary>
        public NotificationStatus Status { get; set; }

        /// <summary>
        /// رسالة الخطأ (في حالة الفشل)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// معرف الرسالة في Telegram (لتتبع الرسالة)
        /// </summary>
        public long? TelegramMessageId { get; set; }
    }
}