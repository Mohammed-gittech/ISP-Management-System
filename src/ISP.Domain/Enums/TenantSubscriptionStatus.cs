// ============================================
// TenantSubscriptionStatus.cs - حالة اشتراك الوكيل
// ============================================
namespace ISP.Domain.Enums
{
    public enum TenantSubscriptionStatus
    {
        Pending = 0,     // ينتظر الدفع
        Active = 1,      // نشط
        Expired = 2,     // منتهي
        Suspended = 3    // معلق (عدم دفع)
    }
}