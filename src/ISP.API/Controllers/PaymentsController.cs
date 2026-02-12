using ISP.Application.DTOs.Payments;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة الدفعات
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPaymentService paymentService,
            ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _logger = logger;
        }

        // ============================================
        // POST: Create Cash Payment
        // ============================================

        /// <summary>
        /// استلام دفعة كاش من المشترك
        /// </summary>
        [HttpPost("cash")]
        public async Task<IActionResult> CreateCashPayment([FromBody] CreateCashPaymentDto dto)
        {
            try
            {
                var result = await _paymentService.ProcessCashPaymentAsync(dto);

                return Ok(new
                {
                    success = true,
                    message = "تم استلام الدفعة بنجاح",
                    data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid cash payment attempt");
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cash payment");
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء معالجة الدفعة"
                });
            }
        }

        // ============================================
        // GET: Payment By ID
        // ============================================

        /// <summary>
        /// جلب دفعة بالـ ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPaymentById(int id)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(id);

            if (payment == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"الدفعة برقم {id} غير موجودة"
                });
            }

            return Ok(new
            {
                success = true,
                data = payment
            });
        }

        // ============================================
        // GET: Subscriber Payments
        // ============================================

        /// <summary>
        /// جلب كل دفعات مشترك معين
        /// </summary>
        [HttpGet("subscriber/{subscriberId}")]
        public async Task<IActionResult> GetSubscriberPayments(int subscriberId)
        {
            var payments = await _paymentService.GetSubscriberPaymentsAsync(subscriberId);

            return Ok(new
            {
                success = true,
                data = payments,
                count = payments.Count
            });
        }

        // ============================================
        // GET: All Payments (Paginated)
        // ============================================

        /// <summary>
        /// جلب كل الدفعات مع Pagination و Filters
        /// </summary>
        /// <param name="page">رقم الصفحة (افتراضي: 1)</param>
        /// <param name="pageSize">عدد العناصر في الصفحة (افتراضي: 10)</param>
        /// <param name="status">فلتر حسب الحالة (اختياري): Completed, Pending, Failed, Refunded</param>
        /// <param name="paymentMethod">فلتر حسب طريقة الدفع (اختياري): Cash, CreditCard, etc</param>
        [HttpGet]
        public async Task<IActionResult> GetPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? paymentMethod = null)
        {
            var result = await _paymentService.GetPaymentsAsync(page, pageSize, status, paymentMethod);

            return Ok(new
            {
                success = true,
                data = result
            });
        }

        // ============================================
        // GET: Payment Statistics
        // ============================================

        /// <summary>
        /// إحصائيات الدفعات
        /// </summary>
        /// <param name="fromDate">من تاريخ (اختياري، افتراضي: آخر شهر)</param>
        /// <param name="toDate">إلى تاريخ (اختياري، افتراضي: اليوم)</param>
        [HttpGet("stats")]
        public async Task<IActionResult> GetPaymentStats(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            var stats = await _paymentService.GetPaymentStatsAsync(fromDate, toDate);

            return Ok(new
            {
                success = true,
                data = stats
            });
        }

        // ============================================
        // POST: Refund Payment
        // ============================================

        /// <summary>
        /// استرداد دفعة (كامل أو جزئي)
        /// </summary>
        [HttpPost("{id}/refund")]
        public async Task<IActionResult> RefundPayment(
            int id,
            [FromBody] RefundPaymentDto dto)
        {
            try
            {
                var result = await _paymentService.RefundPaymentAsync(id, dto.Amount, dto.Reason);

                return Ok(new
                {
                    success = true,
                    message = "تم استرداد الدفعة بنجاح",
                    data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid refund attempt for payment {PaymentId}", id);
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding payment {PaymentId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء استرداد الدفعة"
                });
            }
        }
    }

    /// <summary>
    /// DTO لاسترداد الدفعة
    /// </summary>
    public class RefundPaymentDto
    {
        /// <summary>
        /// المبلغ المراد استرداده (اختياري، إذا لم يُحدد = استرداد كامل)
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// سبب الاسترداد
        /// </summary>
        public string? Reason { get; set; }
    }
}