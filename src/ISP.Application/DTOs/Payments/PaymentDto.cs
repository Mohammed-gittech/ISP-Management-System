// ============================================
// PaymentDto.cs - Payment Response
// ============================================
namespace ISP.Application.DTOs.Payments
{
    /// <summary>
    /// DTO لعرض معلومات الدفعة
    /// </summary>
    public class PaymentDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberName { get; set; } = string.Empty;
        public int? SubscriptionId { get; set; }
        public int? InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "IQD";

        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentGateway { get; set; }
        public string? TransactionId { get; set; }

        public string Status { get; set; } = string.Empty;

        // Cash Payment Info
        public int? ReceivedBy { get; set; }
        public string? ReceivedByName { get; set; }
        public string? CashReceiptNumber { get; set; }

        public string? Notes { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO لعرض قائمة الدفعات (مبسط)
    /// </summary>
    public class PaymentListDto
    {
        public int Id { get; set; }
        public string SubscriberName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "IQD";
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Response بعد إنشاء دفعة
    /// </summary>
    public class CreatePaymentResponseDto
    {
        public int PaymentId { get; set; }
        public int? InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }

        /// <summary>
        /// رابط تحميل الفاتورة PDF
        /// </summary>
        public string? InvoicePdfUrl { get; set; }

        /// <summary>
        /// هل تم تجديد الاشتراك؟
        /// </summary>
        public bool SubscriptionRenewed { get; set; }

        /// <summary>
        /// تاريخ انتهاء الاشتراك الجديد (إذا تم التجديد)
        /// </summary>
        public DateTime? NewSubscriptionEndDate { get; set; }
    }
}