using ISP.Domain.Entities;

namespace ISP.Application.Interfaces;

/// <summary>
/// واجهة خدمة Telegram Bot
/// تتعامل مع جميع عمليات الإرسال عبر Telegram
/// </summary>
public interface ITelegramService
{
    /// <summary>
    /// إرسال رسالة نصية بسيطة
    /// </summary>
    /// <param name="chatId">معرف المحادثة في Telegram</param>
    /// <param name="message">نص الرسالة</param>
    /// <returns>true إذا نجح الإرسال، false إذا فشل</returns>
    Task<bool> SendMessageAsync(int tenantId, string chatId, string message);

    /// <summary>
    /// إرسال تنبيه قبل انتهاء الاشتراك
    /// </summary>
    /// <param name="subscription">بيانات الاشتراك الكاملة (مع Subscriber و Plan)</param>
    /// <param name="daysRemaining">عدد الأيام المتبقية حتى الانتهاء</param>
    /// <returns>true إذا نجح الإرسال، false إذا فشل</returns>
    Task<bool> SendExpiryWarningAsync(int tenantId, Subscription subscription, int daysRemaining);

    /// <summary>
    /// إرسال تأكيد تجديد الاشتراك
    /// </summary>
    /// <param name="subscription">بيانات الاشتراك المجدد (مع Subscriber و Plan)</param>
    /// <returns>true إذا نجح الإرسال، false إذا فشل</returns>
    Task<bool> SendRenewalConfirmationAsync(int tenantId, Subscription subscription);

    /// <summary>
    /// إرسال إشعار بانتهاء الاشتراك (بعد الانتهاء الفعلي)
    /// </summary>
    /// <param name="subscription">بيانات الاشتراك المنتهي</param>
    /// <returns>true إذا نجح الإرسال، false إذا فشل</returns>
    Task<bool> SendExpiredNotificationAsync(int tenantId, Subscription subscription);

    /// <summary>
    /// اختبار الاتصال بـ Telegram Bot والتحقق من صحة Token
    /// </summary>
    /// <param name="botToken">Token الخاص بالبوت</param>
    /// <returns>true إذا كان Token صحيح والاتصال نجح، false إذا فشل</returns>
    Task<bool> TestConnectionAsync(string botToken);
}