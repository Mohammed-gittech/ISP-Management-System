// ============================================
// NotificationType.cs - نوع الإشعار
// ============================================
namespace ISP.Domain.Enums
{
    public enum NotificationType
    {
        ExpiryWarning = 0,      // تنبيه انتهاء الاشتراك
        PaymentReminder = 1,    // تذكير دفع
        SubscriptionRenewed = 2, // تم التجديد
        SystemAlert = 3         // تنبيه النظام
    }
}