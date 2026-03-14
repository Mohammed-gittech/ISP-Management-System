using ISP.Application.DTOs.SecurityAlerts;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Manages security alerts — SuperAdmin only
    /// </summary>
    [ApiController]
    [Route("api/security-alerts")]
    [Authorize(Roles = "SuperAdmin")]
    public class SecurityAlertsController : ControllerBase
    {
        private readonly ISecurityAlertService _securityAlertService;
        private readonly ILogger<SecurityAlertsController> _logger;

        public SecurityAlertsController(
            ISecurityAlertService securityAlertService,
            ILogger<SecurityAlertsController> logger)
        {
            _securityAlertService = securityAlertService;
            _logger = logger;
        }

        /// <summary>
        /// Returns all security alerts with optional status filter
        /// GET /api/security-alerts?status=New
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAlerts([FromQuery] string? status = null)
        {
            var alerts = await _securityAlertService.GetAlertsAsync(status);

            return Ok(new
            {
                success = true,
                count = alerts.Count(),
                data = alerts
            });
        }

        /// <summary>
        /// Marks an alert as reviewed
        /// POST /api/security-alerts/{id}/review
        /// </summary>
        [HttpPost("{id}/review")]
        public async Task<IActionResult> MarkAsReviewed(
            int id,
            [FromBody] ReviewAlertDto? dto = null)
        {
            var result = await _securityAlertService
                .MarkAsReviewedAsync(id, dto?.Notes);

            if (!result)
                return NotFound(new { success = false, message = "التنبيه غير موجود" });

            return Ok(new { success = true, message = "تم تحديد التنبيه كمراجع" });
        }

        /// <summary>
        /// Marks an alert as resolved
        /// POST /api/security-alerts/{id}/resolve
        /// </summary>
        [HttpPost("{id}/resolve")]
        public async Task<IActionResult> MarkAsResolved(int id)
        {
            var result = await _securityAlertService.MarkAsResolvedAsync(id);

            if (!result)
                return NotFound(new { success = false, message = "التنبيه غير موجود" });

            return Ok(new { success = true, message = "تم تحديد التنبيه كمحلول" });
        }

        /// <summary>
        /// Marks an alert as ignored (false positive)
        /// POST /api/security-alerts/{id}/ignore
        /// </summary>
        [HttpPost("{id}/ignore")]
        public async Task<IActionResult> MarkAsIgnored(int id)
        {
            var result = await _securityAlertService.MarkAsIgnoredAsync(id);

            if (!result)
                return NotFound(new { success = false, message = "التنبيه غير موجود" });

            return Ok(new { success = true, message = "تم تجاهل التنبيه" });
        }

        /// <summary>
        /// Manually triggers a security scan — for testing purposes
        /// POST /api/security-alerts/scan
        /// </summary>
        [HttpPost("scan")]
        public async Task<IActionResult> TriggerScan()
        {
            _logger.LogWarning(
                "Manual security scan triggered by SuperAdmin");

            await _securityAlertService.DetectAndAlertAsync();

            return Ok(new { success = true, message = "Security scan completed" });
        }
    }
}