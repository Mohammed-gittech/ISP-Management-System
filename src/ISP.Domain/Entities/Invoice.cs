// ============================================
// Invoice.cs - فواتير المشتركين
// ============================================
namespace ISP.Domain.Entities
{
    /// <summary>
    /// الفاتورة - تُصدر للمشترك عند الدفع
    /// </summary>
    public class Invoice : BaseEntity
    {
        // ============================================
        // Foreign Keys
        // ============================================

        public int TenantId { get; set; }

        /// <summary>
        /// معرف المشترك (null للفواتير العامة)
        /// </summary>
        public int? SubscriberId { get; set; }

        public int? PaymentId { get; set; } // null إذا لم تُدفع بعد

        // ============================================
        // Invoice Info
        // ============================================

        /// <summary>
        /// رقم الفاتورة الفريد (INV-2024-00001)
        /// </summary>
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>
        /// عناصر الفاتورة (JSON)
        /// [{"name": "باقة 50 ميجا", "quantity": 1, "price": 15000}]
        /// </summary>
        public string? Items { get; set; }

        // ============================================
        // Amounts
        // ============================================

        /// <summary>
        /// المجموع الفرعي (قبل الضريبة والخصم)
        /// </summary>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// الضريبة (اختياري)
        /// </summary>
        public decimal Tax { get; set; } = 0;

        /// <summary>
        /// الخصم
        /// </summary>
        public decimal Discount { get; set; } = 0;

        /// <summary>
        /// الإجمالي النهائي
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// العملة
        /// </summary>
        public string Currency { get; set; } = "IQD";

        // ============================================
        // Status & Dates
        // ============================================

        /// <summary>
        /// الحالة (Draft, Unpaid, Paid, Cancelled, Refunded)
        /// </summary>
        public string Status { get; set; } = Domain.Enums.InvoiceStatus.Unpaid.ToString();

        /// <summary>
        /// تاريخ الاستحقاق
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// تاريخ الإصدار
        /// </summary>
        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// تاريخ الدفع
        /// </summary>
        public DateTime? PaidDate { get; set; }

        // ============================================
        // Printing Info
        // ============================================

        /// <summary>
        /// تاريخ آخر طباعة
        /// </summary>
        public DateTime? PrintedAt { get; set; }

        /// <summary>
        /// عدد مرات الطباعة
        /// </summary>
        public int PrintCount { get; set; } = 0;

        // ============================================
        // Additional Info
        // ============================================

        /// <summary>
        /// ملاحظات
        /// </summary>
        public string? Notes { get; set; }

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
        public Payment? Payment { get; set; }
    }
}