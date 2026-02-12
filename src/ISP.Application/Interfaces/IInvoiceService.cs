// ============================================
// IInvoiceService.cs
// ============================================
using ISP.Application.DTOs;
using ISP.Application.DTOs.Invoices;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// خدمة إدارة الفواتير
    /// </summary>
    public interface IInvoiceService
    {
        // ============================================
        // Invoice Creation
        // ============================================    

        /// <summary>
        /// جلب فاتورة بالـ ID
        /// </summary>
        Task<InvoiceDto?> GetInvoiceByIdAsync(int invoiceId);

        /// <summary>
        /// جلب فاتورة برقم الفاتورة
        /// </summary>
        Task<InvoiceDto?> GetInvoiceByNumberAsync(string invoiceNumber);

        /// <summary>
        /// جلب كل فواتير مشترك معين
        /// </summary>
        Task<List<InvoiceDto>> GetSubscriberInvoicesAsync(int subscriberId);

        /// <summary>
        /// جلب كل فواتير الـ Tenant (مع Pagination)
        /// </summary>
        Task<PagedResultDto<InvoiceListDto>> GetInvoicesAsync(int pageNumber = 1, int pageSize = 10, string? status = null);

        // ============================================
        // PDF Generation
        // ============================================

        /// <summary>
        /// توليد PDF للفاتورة (طباعة حرارية 80mm)
        /// </summary>
        Task<byte[]> GenerateInvoicePdfAsync(int invoiceId);

        /// <summary>
        /// جلب بيانات الفاتورة للطباعة
        /// </summary>
        Task<InvoicePrintDto> GetInvoicePrintDataAsync(int invoiceId);

        // ============================================
        // Print Tracking
        // ============================================

        /// <summary>
        /// تسجيل طباعة الفاتورة
        /// </summary>
        Task RecordPrintAsync(int invoiceId);

        // ============================================
        // Invoice Management
        // ============================================

        /// <summary>
        /// إلغاء فاتورة
        /// </summary>
        Task CancelInvoiceAsync(int invoiceId, string? reason = null);
    }
}