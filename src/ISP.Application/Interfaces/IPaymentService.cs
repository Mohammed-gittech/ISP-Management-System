// ============================================
// IPaymentService.cs
// ============================================
using ISP.Application.DTOs;
using ISP.Application.DTOs.Payments;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// خدمة إدارة الدفعات
    /// </summary>
    public interface IPaymentService
    {
        // ============================================
        // Cash Payments
        // ============================================

        /// <summary>
        /// معالجة دفعة كاش من المشترك
        /// </summary>
        Task<CreatePaymentResponseDto> ProcessCashPaymentAsync(CreateCashPaymentDto dto);

        // ============================================
        // Payment Queries
        // ============================================

        /// <summary>
        /// جلب دفعة بالـ ID
        /// </summary>
        Task<PaymentDto?> GetPaymentByIdAsync(int paymentId);

        /// <summary>
        /// جلب كل دفعات مشترك معين
        /// </summary>
        Task<List<PaymentDto>> GetSubscriberPaymentsAsync(int subscriberId);

        /// <summary>
        /// جلب كل دفعات الـ Tenant (مع Pagination)
        /// </summary>
        Task<PagedResultDto<PaymentListDto>> GetPaymentsAsync(int pageNumber = 1, int pageSize = 10, string? status = null, string? paymentMethod = null);

        /// <summary>
        /// إحصائيات الدفعات
        /// </summary>
        Task<PaymentStatsDto> GetPaymentStatsAsync(DateTime? fromDate = null, DateTime? toDate = null);

        // ============================================
        // Refunds (لاحقاً)
        // ============================================

        /// <summary>
        /// استرداد دفعة
        /// </summary>
        Task<PaymentDto> RefundPaymentAsync(int paymentId, decimal? amount = null, string? reason = null);
    }

    /// <summary>
    /// إحصائيات الدفعات
    /// </summary>
    public class PaymentStatsDto
    {
        public decimal TotalAmount { get; set; }
        public int TotalCount { get; set; }
        public decimal CashAmount { get; set; }
        public int CashCount { get; set; }
        public decimal OnlineAmount { get; set; }
        public int OnlineCount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}