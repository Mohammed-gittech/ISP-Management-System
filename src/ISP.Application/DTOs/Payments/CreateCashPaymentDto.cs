// ============================================
// CreateCashPaymentDto.cs - Cash Payment Request
// ============================================
using ISP.Application.DTOs.Invoices;

namespace ISP.Application.DTOs.Payments
{
    /// <summary>
    /// DTO لإنشاء دفعة كاش
    /// يستخدمه الوكيل عندما يستلم مال من المشترك
    /// </summary>
    public class CreateCashPaymentDto
    {
        /// <summary>
        /// معرف المشترك
        /// </summary>
        public int SubscriberId { get; set; }

        /// <summary>
        /// معرف الاشتراك (اختياري - قد تكون دفعة عامة)
        /// </summary>
        public int? SubscriptionId { get; set; }

        /// <summary>
        /// المبلغ المدفوع
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// العملة (IQD, USD, EUR)
        /// </summary>
        public string Currency { get; set; } = "IQD";

        /// <summary>
        /// رقم الإيصال الورقي (اختياري)
        /// </summary>
        public string? CashReceiptNumber { get; set; }

        /// <summary>
        /// ملاحظات
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// هل نريد إنشاء فاتورة؟ (افتراضياً: نعم)
        /// </summary>
        public bool GenerateInvoice { get; set; } = true;

        /// <summary>
        /// عناصر الفاتورة (اختياري - إذا لم يُحدد، سيُستخدم الاشتراك)
        /// </summary>
        public List<InvoiceItemDto>? InvoiceItems { get; set; }
    }

}