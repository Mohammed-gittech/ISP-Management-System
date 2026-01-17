// ============================================
// NotificationStatus.cs - حالة الإشعار
// ============================================
namespace ISP.Domain.Enums
{
    public enum NotificationStatus
    {
        Pending = 0,     // في الانتظار
        Sent = 1,        // تم الإرسال
        Failed = 2       // فشل
    }
}