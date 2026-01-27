using ISP.Domain.Entities;
using ISP.Domain.Enums;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// واجهة خدمة إدارة الإشعارات
    /// تتعامل مع إنشاء وإرسال وإدارة كل أنواع الإشعارات
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// إنشاء وحفظ إشعار جديد في Database
        /// </summary>
        /// <param name="tenantId">معرف الوكيل</param>
        /// <param name="subscriberId">معرف المشترك</param>
        /// <param name="type">نوع الإشعار (ExpiryWarning, PaymentReminder, etc.)</param>
        /// <param name="message">نص الرسالة</param>
        /// <param name="channel">قناة الإرسال (Telegram, WhatsApp, Email, SMS)</param>
        /// <returns>الإشعار المُنشأ (Status: Pending)</returns>
        Task<Notification> CreateNotificationAsync(
            int tenantId,
            int subscriberId,
            NotificationType type,
            string message,
            NotificationChannel channel = NotificationChannel.Telegram
        );

        /// <summary>
        /// إرسال إشعار موجود مسبقاً في Database
        /// يحدث حالة الإشعار بناءً على نتيجة الإرسال
        /// </summary>
        /// <param name="notificationId">معرف الإشعار</param>
        /// <returns>true إذا نجح الإرسال، false إذا فشل</returns>
        Task<bool> SendNotificationAsync(int notificationId);

        /// <summary>
        /// إنشاء وإرسال إشعار في خطوة واحدة
        /// </summary>
        /// <param name="tenantId">معرف الوكيل</param>
        /// <param name="subscriberId">معرف المشترك</param>
        /// <param name="type">نوع الإشعار</param>
        /// <param name="message">نص الرسالة</param>
        /// <param name="channel">قناة الإرسال</param>
        /// <returns>true إذا نجح الإنشاء والإرسال، false إذا فشل</returns>
        Task<bool> CreateAndSendAsync(
            int tenantId,
            int subscriberId,
            NotificationType type,
            string message,
            NotificationChannel channel = NotificationChannel.Telegram
        );

        /// <summary>
        /// إعادة محاولة إرسال إشعار فاشل
        /// يُستخدم في Background Jobs للـ Retry Mechanism
        /// </summary>
        /// <param name="notificationId">معرف الإشعار الفاشل</param>
        /// <returns>true إذا نجحت إعادة المحاولة، false إذا فشلت</returns>
        Task<bool> RetryFailedNotificationAsync(int notificationId);

        /// <summary>
        /// الحصول على جميع الإشعارات الفاشلة لوكيل معين
        /// يُستخدم في Background Job للـ Retry
        /// </summary>
        /// <param name="tenantId">معرف الوكيل (optional - null = كل الوكلاء)</param>
        /// <param name="maxRetries">عدد محاولات إعادة الإرسال القصوى (default: 3)</param>
        /// <returns>قائمة الإشعارات الفاشلة التي لم تتجاوز MaxRetries</returns>
        Task<IEnumerable<Notification>> GetFailedNotificationsAsync(int? tenantId = null, int maxRetries = 3);

        /// <summary>
        /// الحصول على إشعار بالـ Id (مع Subscriber و Tenant)
        /// </summary>
        Task<Notification?> GetByIdAsync(int notificationId);

        /// <summary>
        /// الحصول على آخر إشعار لمشترك معين
        /// </summary>
        Task<Notification?> GetLatestForSubscriberAsync(int subscriberId, NotificationType? type = null);

    }
}