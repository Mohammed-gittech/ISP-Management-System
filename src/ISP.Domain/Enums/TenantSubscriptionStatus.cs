// ============================================
// TenantSubscriptionStatus.cs - حالة اشتراك الوكيل
// ============================================
namespace ISP.Domain.Enums
{
    public enum TenantSubscriptionStatus
    {
        Active = 0,      // نشط
        Expired = 1,     // منتهي
        Suspended = 2    // معلق (عدم دفع)
    }
}