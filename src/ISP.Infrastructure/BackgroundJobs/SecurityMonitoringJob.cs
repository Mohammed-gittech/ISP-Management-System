using ISP.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background job that periodically scans for suspicious activity
    /// Runs every minute via Hangfire
    /// </summary>
    public class SecurityMonitoringJob
    {
        private readonly ISecurityAlertService _securityAlertService;
        private readonly ILogger<SecurityMonitoringJob> _logger;

        public SecurityMonitoringJob(
            ISecurityAlertService securityAlertService,
            ILogger<SecurityMonitoringJob> logger)
        {
            _securityAlertService = securityAlertService;
            _logger = logger;
        }

        /// <summary>
        /// Main entry point called by Hangfire every minute
        /// Runs security scan and logs results
        /// </summary>
        public async Task RunSecurityScanAsync()
        {
            _logger.LogInformation("Security monitoring scan started | Time:{Time}",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            try
            {
                // Delegate detection logic to SecurityAlertService
                await _securityAlertService.DetectAndAlertAsync();

                _logger.LogInformation("Security monitoring scan completed | Time:{Time}",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                // Log error but don't throw — job must not crash Hangfire
                _logger.LogError(ex,
                    "Security monitoring scan failed | Time:{Time}",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }


    }
}