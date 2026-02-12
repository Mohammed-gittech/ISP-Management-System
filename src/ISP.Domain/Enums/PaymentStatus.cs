// ============================================
// PaymentStatus.cs
// ============================================
namespace ISP.Domain.Enums
{
    /// <summary>
    /// حالات الدفع
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>
        /// معلّق (في انتظار التأكيد)
        /// </summary>
        Pending = 1,

        /// <summary>
        /// مكتمل (تم الدفع بنجاح)
        /// </summary>
        Completed = 2,

        /// <summary>
        /// فشل
        /// </summary>
        Failed = 3,

        /// <summary>
        /// تم الاسترداد
        /// </summary>
        Refunded = 4,

        /// <summary>
        /// ملغي
        /// </summary>
        Cancelled = 5
    }
}