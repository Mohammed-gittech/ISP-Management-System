using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller للتقارير والإحصائيات
    /// يوفر تقارير مالية وإدارية للوكيل
    /// </summary>

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        // ============================================
        // Dependencies
        // ============================================
        private readonly IReportService _reportService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        /// <summary>
        /// الحصول على تقرير الإيرادات
        /// </summary>
        [HttpGet("revenue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRevenueReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                // Step 1: Logging (تسجيل الطلب)
                _logger.LogInformation(
                    "Revenue report requested - StartDate: {StartDate}, EndDate: {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "All",
                    endDate?.ToString("yyyy-MM-dd") ?? "Now"
                );

                // Step 2: Call Service (استدعاء الخدمة)
                var report = await _reportService.GetRevenueReportAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    message = "تم إنشاء التقرير بنجاح",
                    data = report
                });
            }
            catch (Exception ex)
            {
                // Error Handling (معالجة الأخطاء)
                _logger.LogError(ex, "Error generating revenue report");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "حدث خطأ أثناء إنشاء التقرير",
                        error = ex.Message
                    }
                );
            }
        }

        /// <summary>
        /// الحصول على تقرير نمو المشتركين
        /// </summary>
        [HttpGet("growth")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetGrowthReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation(
                    "Growth report requested - StartDate: {StartDate}, EndDate: {EndDate}",
                    startDate?.ToString("yyyy-MM-dd") ?? "All",
                    endDate?.ToString("yyyy-MM-dd") ?? "Now"
                );

                var report = await _reportService.GetGrowthReportAsync(startDate, endDate);

                return Ok(new
                {
                    success = true,
                    message = "تم انشاء تقرير النمو بنجاح",
                    data = report
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating growth report");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "حدث خطأ أثناء إنشاء تقرير النمو",
                        error = ex.Message
                    }
                );
            }
        }

        /// <summary>
        /// الحصول على تقرير شعبية الباقات
        /// </summary>
        [HttpGet("plan-popularity")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPlanPopularity(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int? top = null)
        {
            try
            {
                // Step 1: Logging
                _logger.LogInformation(
                    "Plan Popularity report requested - StartDate: {StartDate}, EndDate: {EndDate}, Top: {Top}",
                    startDate?.ToString("yyyy-MM-dd") ?? "All",
                    endDate?.ToString("yyyy-MM-dd") ?? "Now",
                    top?.ToString() ?? "All"
                );

                // Step 2: Call Service
                var report = await _reportService.GetPlanPopularityReportAsync(startDate, endDate, top);

                // Step 3: Return Success Response
                return Ok(new
                {
                    success = true,
                    message = "تم إنشاء تقرير شعبية الباقات بنجاح",
                    data = report
                });
            }
            catch (Exception ex)
            {
                // Error Handling
                _logger.LogError(ex, "Error generating plan popularity report");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "حدث خطأ أثناء إنشاء تقرير شعبية الباقات",
                        error = ex.Message
                    }
                );
            }
        }

        /// <summary>
        /// الحصول على تقرير الاشتراكات المنتهية قريباً
        /// </summary>
        [HttpGet("expiring-soon")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetExpiringSoon([FromQuery] int? days = 7)
        {
            try
            {
                _logger.LogInformation(
                    "Expiring Soon report requested - Days: {Days}",
                    days ?? 7
                );

                var report = await _reportService.GetExpiringSoonReportAsync(days);

                return Ok(new
                {
                    success = true,
                    message = "تم إنشاء تقرير الاشتراكات المنتهية قريباً بنجاح",
                    data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating expiring soon report");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "حدث خطأ أثناء إنشاء تقرير الاشتراكات المنتهية قريباً",
                        error = ex.Message
                    }
                );
            }
        }

        /// <summary>
        /// الحصول على ملخص Dashboard - نظرة شاملة سريعة
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                _logger.LogInformation("Dashboard summary requested");

                var dashboard = await _reportService.GetDashboardSummaryAsync();

                return Ok(new
                {
                    success = true,
                    message = "بنجاح Dashboard تم انشاء ملخص",
                    data = dashboard
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating dashboard summary");

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "Dashboard حدث خطأ أثناء إنشاء ملخص",
                        error = ex.Message
                    });
            }
        }
    }
}