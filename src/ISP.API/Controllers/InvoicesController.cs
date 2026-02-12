using ISP.Application.DTOs.Invoices;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الفواتير
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoicesController> _logger;

        public InvoicesController(
            IInvoiceService invoiceService,
            ILogger<InvoicesController> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        // ============================================
        // GET: Invoice By ID
        // ============================================

        /// <summary>
        /// جلب فاتورة بالـ ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetInvoiceById(int id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);

            if (invoice == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"الفاتورة برقم {id} غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                data = invoice
            });
        }

        // ============================================
        // GET: Invoice By Number
        // ============================================

        /// <summary>
        /// جلب فاتورة برقم الفاتورة
        /// </summary>
        /// <param name="number">رقم الفاتورة (مثال: INV-2024-00001)</param>
        [HttpGet("number/{number}")]
        public async Task<IActionResult> GetInvoiceByNumber(string number)
        {
            var invoice = await _invoiceService.GetInvoiceByNumberAsync(number);

            if (invoice == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"الفاتورة برقم {number} غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                data = invoice
            });
        }

        // ============================================
        // GET: Subscriber Invoices
        // ============================================

        /// <summary>
        /// جلب كل فواتير مشترك معين
        /// </summary>
        [HttpGet("subscriber/{subscriberId}")]
        public async Task<IActionResult> GetSubscriberInvoices(int subscriberId)
        {
            var invoices = await _invoiceService.GetSubscriberInvoicesAsync(subscriberId);

            return Ok(new
            {
                success = true,
                data = invoices,
                count = invoices.Count
            });
        }

        // ============================================
        // GET: All Invoices (Paginated)
        // ============================================

        /// <summary>
        /// جلب كل الفواتير مع Pagination و Filter
        /// </summary>
        /// <param name="page">رقم الصفحة (افتراضي: 1)</param>
        /// <param name="pageSize">عدد العناصر (افتراضي: 10)</param>
        /// <param name="status">فلتر حسب الحالة (اختياري): Paid, Unpaid, Cancelled, etc</param>
        [HttpGet]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var result = await _invoiceService.GetInvoicesAsync(page, pageSize, status);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        // ============================================
        // GET: Invoice PDF
        // ============================================

        /// <summary>
        /// تحميل الفاتورة كـ PDF (طباعة حرارية 80mm)
        /// </summary>
        /// <remarks>
        /// يُرجع ملف PDF جاهز للطباعة الحرارية 80mm
        /// 
        /// الميزات:
        /// - دعم كامل للعربية (RTL)
        /// - QR Code للتحقق
        /// - تصميم مُحسّن للوكلاء
        /// - حجم مناسب (80mm × طول تلقائي)
        /// 
        /// استخدام:
        /// - عرض: window.open('/api/invoices/123/pdf')
        /// - تحميل: &lt;a href="/api/invoices/123/pdf" download&gt;
        /// - طباعة: fetch ثم window.print()
        /// </remarks>
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetInvoicePdf(int id)
        {
            try
            {
                var pdfBytes = await _invoiceService.GenerateInvoicePdfAsync(id);

                return File(pdfBytes, "application/pdf", $"invoice-{id}.pdf");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invoice {InvoiceId} not found", id);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for invoice {InvoiceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء إنشاء PDF"
                });
            }
        }

        // ============================================
        // GET: Invoice Print Data
        // ============================================

        /// <summary>
        /// جلب بيانات الفاتورة للطباعة (مع معلومات الشركة)
        /// </summary>
        /// <remarks>
        /// يُستخدم في Frontend لطباعة الفاتورة عبر المتصفح
        /// يحتوي على:
        /// - معلومات الشركة (Logo, Address, Phone)
        /// - معلومات الفاتورة الكاملة
        /// - QR Code data
        /// - Barcode data
        /// </remarks>
        [HttpGet("{id}/print-data")]
        public async Task<IActionResult> GetInvoicePrintData(int id)
        {
            try
            {
                var printData = await _invoiceService.GetInvoicePrintDataAsync(id);

                return Ok(new
                {
                    success = true,
                    data = printData
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invoice {InvoiceId} not found", id);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting print data for invoice {InvoiceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ"
                });
            }
        }

        // ============================================
        // POST: Record Print
        // ============================================

        /// <summary>
        /// تسجيل طباعة الفاتورة
        /// </summary>
        /// <remarks>
        /// يُستدعى تلقائياً عند طباعة الفاتورة
        /// يزيد PrintCount ويحدث PrintedAt
        /// </remarks>
        [HttpPost("{id}/record-print")]
        public async Task<IActionResult> RecordPrint(int id)
        {
            try
            {
                await _invoiceService.RecordPrintAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "تم تسجيل الطباعة"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invoice {InvoiceId} not found", id);
                return NotFound(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording print for invoice {InvoiceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ"
                });
            }
        }

        // ============================================
        // DELETE: Cancel Invoice
        // ============================================

        /// <summary>
        /// إلغاء فاتورة
        /// </summary>
        /// <remarks>
        /// ملاحظة: لا يمكن إلغاء فاتورة مدفوعة
        /// استخدم Refund بدلاً من ذلك
        /// </remarks>
        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelInvoice(int id, [FromQuery] string? reason = null)
        {
            try
            {
                await _invoiceService.CancelInvoiceAsync(id, reason);

                return Ok(new
                {
                    success = true,
                    message = "تم إلغاء الفاتورة"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Cannot cancel invoice {InvoiceId}", id);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling invoice {InvoiceId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ"
                });
            }
        }
    }
}