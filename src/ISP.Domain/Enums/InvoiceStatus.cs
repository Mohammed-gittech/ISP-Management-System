// ============================================
// InvoiceStatus.cs
// ============================================
namespace ISP.Domain.Enums
{
    /// <summary>
    /// حالات الفاتورة
    /// </summary>
    public enum InvoiceStatus
    {
        /// <summary>
        /// مسودة
        /// </summary>
        Draft = 1,

        /// <summary>
        /// غير مدفوعة
        /// </summary>
        Unpaid = 2,

        /// <summary>
        /// مدفوعة
        /// </summary>
        Paid = 3,

        /// <summary>
        /// ملغاة
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// مستردة
        /// </summary>
        Refunded = 5,

        /// <summary>
        /// متأخرة (تجاوزت DueDate)
        /// </summary>
        Overdue = 6
    }
}