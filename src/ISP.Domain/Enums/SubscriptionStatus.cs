// ============================================
// SubscriptionStatus.cs - حالة الاشتراك
// ============================================
namespace ISP.Domain.Enums
{
    public enum SubscriptionStatus
    {
        Active = 0,      // نشط
        Expiring = 1,    // على وشك الانتهاء (خلال 7 أيام)
        Expired = 2      // منتهي
    }
}