// ============================================
// NotificationChannel.cs - قناة الإرسال
// ============================================
namespace ISP.Domain.Enums
{
    public enum NotificationChannel
    {
        Telegram = 0,    // أساسي
        WhatsApp = 1,    // اختياري
        Email = 2,       // اختياري
        SMS = 3          // اختياري
    }
}