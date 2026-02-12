// ============================================
// Payment.cs - دفعات المشتركين للوكلاء
// ============================================
namespace ISP.Domain.Entities
{
    /// <summary>
    /// دفعات المشتركين (Cash, Online, Bank Transfer, etc)
    /// المشترك يدفع للوكيل عبر النظام أو كاش
    /// </summary>
    public class Payment : BaseEntity
    {
        // ============================================
        // Foreign Keys
        // ============================================

        public int TenantId { get; set; }
        public int SubscriberId { get; set; }
        public int? SubscriptionId { get; set; } // اختياري (قد تكون دفعة عامة)
        public int? InvoiceId { get; set; }

        // ============================================
        // Payment Details
        // ============================================

        /// <summary>
        /// المبلغ
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// العملة (IQD, USD, EUR)
        /// </summary>
        public string Currency { get; set; } = "IQD";

        /// <summary>
        /// طريقة الدفع (Cash, Online, BankTransfer, Wallet)
        /// </summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// بوابة الدفع (Stripe, ZainCash, PayPal) - null للكاش
        /// </summary>
        public string? PaymentGateway { get; set; }

        /// <summary>
        /// Transaction ID من البوابة - null للكاش
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// الحالة (Pending, Completed, Failed, Refunded)
        /// </summary>
        public string Status { get; set; } = "Pending";

        // ============================================
        // Cash Payment Specific
        // ============================================

        /// <summary>
        /// الموظف الذي استلم المال (UserId) - للكاش فقط
        /// </summary>
        public int? ReceivedBy { get; set; }

        /// <summary>
        /// رقم الإيصال الورقي (اختياري)
        /// </summary>
        public string? CashReceiptNumber { get; set; }

        // ============================================
        // Additional Info
        // ============================================

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
        public Subscriber? Subscriber { get; set; }
        public Subscription? Subscription { get; set; }
        public Invoice? Invoice { get; set; }
        public User? ReceivedByUser { get; set; }
    }
}