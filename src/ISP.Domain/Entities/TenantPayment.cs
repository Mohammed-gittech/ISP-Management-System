// ============================================
// TenantPayment.cs - دفعات الوكلاء (SaaS Subscription)
// ============================================
namespace ISP.Domain.Entities
{
    /// <summary>
    /// دفعات الوكلاء - الوكيل يدفع لصاحب المنصة
    /// (SaaS Subscription Model)
    /// </summary>
    public class TenantPayment : BaseEntity
    {
        // ============================================
        // Foreign Keys
        // ============================================

        public int TenantId { get; set; }
        public int TenantSubscriptionId { get; set; }

        // ============================================
        // Payment Details
        // ============================================

        /// <summary>
        /// المبلغ
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// العملة (USD, EUR, IQD)
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// طريقة الدفع (CreditCard, BankTransfer, PayPal)
        /// </summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// بوابة الدفع (Stripe, PayPal, ZainCash)
        /// </summary>
        public string PaymentGateway { get; set; } = string.Empty;

        /// <summary>
        /// Transaction ID من البوابة
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// الحالة (Pending, Completed, Failed, Refunded)
        /// </summary>
        public string Status { get; set; } = "Pending";

        // ============================================
        // Additional Info
        // ============================================

        /// <summary>
        /// Invoice URL من Stripe (اختياري)
        /// </summary>
        public string? InvoiceUrl { get; set; }

        /// <summary>
        /// Receipt URL من Stripe (اختياري)
        /// </summary>
        public string? ReceiptUrl { get; set; }

        /// <summary>
        /// ملاحظات
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// تاريخ الدفع
        /// </summary>
        public DateTime? PaidAt { get; set; }

        /// <summary>
        /// تاريخ الإنشاء
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// تاريخ آخر تحديث
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        // ============================================
        // Navigation Properties
        // ============================================

        public Tenant? Tenant { get; set; }
        public TenantSubscription? TenantSubscription { get; set; }
    }
}