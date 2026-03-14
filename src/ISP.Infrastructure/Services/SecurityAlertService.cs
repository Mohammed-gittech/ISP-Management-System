using AutoMapper;
using ISP.Application.DTOs.SecurityAlerts;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    public class SecurityAlertService : ISecurityAlertService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITelegramAlertSender _telegramAlertSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SecurityAlertService> _logger;
        private readonly IMapper _mapper;

        public SecurityAlertService(
            IUnitOfWork unitOfWork,
            ITelegramAlertSender telegramAlertSender,
            IConfiguration configuration,
            ILogger<SecurityAlertService> logger,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _telegramAlertSender = telegramAlertSender;
            _configuration = configuration;
            _logger = logger;
            _mapper = mapper;
        }

        /// <summary>
        /// Main detection method — analyzes recent audit logs for suspicious activity
        /// </summary>
        public async Task DetectAndAlertAsync()
        {
            // Read monitoring window from configuration
            var windowMinutes = _configuration.GetValue<int>(
                "SecurityAlerts:MonitoringWindowMinutes", 10);

            // Define the time window to analyze
            var since = DateTime.UtcNow.AddMinutes(-windowMinutes);

            // Fetch all audit logs within the monitoring window
            var recentLogs = await _unitOfWork.AuditLogs
                .GetAllAsync(l => l.Timestamp >= since);

            _logger.LogInformation(
                "Security scan started | Window:{Window}min | Logs:{Count}",
                windowMinutes, recentLogs.Count());

            // Run all detection rules
            await DetectMultipleFailedLoginsAsync(recentLogs, since);
            await DetectUnauthorizedAccessAsync(recentLogs, since);
            await DetectSuspiciousIpActivityAsync(recentLogs, since);
        }

        /// <summary>
        /// Returns all security alerts with optional status filter
        /// </summary>
        public async Task<IEnumerable<SecurityAlertDto>> GetAlertsAsync(string? status = null)
        {
            // Return all alerts if no status filter provided
            var alerts = string.IsNullOrEmpty(status)
                ? await _unitOfWork.SecurityAlerts.GetAllAsync()
                : await _unitOfWork.SecurityAlerts.GetAllAsync(a => a.Status == status);

            // Map using AutoMapper — consistent with project pattern
            return _mapper.Map<IEnumerable<SecurityAlertDto>>(alerts);
        }

        /// <summary>
        /// Marks an alert as reviewed with optional notes from SuperAdmin
        /// </summary>
        public async Task<bool> MarkAsReviewedAsync(int alertId, string? notes = null)
        {
            var alert = await _unitOfWork.SecurityAlerts.GetByIdAsync(alertId);

            if (alert == null)
            {
                _logger.LogWarning(
                    "MarkAsReviewed failed — alert not found | AlertId:{AlertId}",
                    alertId);
                return false;
            }

            // Update alert status
            alert.Status = "Reviewed";
            alert.ReviewedAt = DateTime.UtcNow;
            alert.ReviewNotes = notes;

            await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Alert marked as reviewed | AlertId:{AlertId} | Notes:{Notes}",
                alertId, notes);

            return true;
        }

        /// <summary>
        /// Marks an alert as resolved — threat has been handled
        /// </summary>
        public async Task<bool> MarkAsResolvedAsync(int alertId)
        {
            var alert = await _unitOfWork.SecurityAlerts.GetByIdAsync(alertId);

            if (alert == null)
            {
                _logger.LogWarning(
                    "MarkAsResolved failed — alert not found | AlertId:{AlertId}",
                    alertId);
                return false;
            }

            // Update alert status
            alert.Status = "Resolved";
            alert.ReviewedAt = DateTime.UtcNow;

            await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Alert marked as resolved | AlertId:{AlertId}",
                alertId);

            return true;
        }

        /// <summary>
        /// Marks an alert as ignored — false positive
        /// </summary>
        public async Task<bool> MarkAsIgnoredAsync(int alertId)
        {
            var alert = await _unitOfWork.SecurityAlerts.GetByIdAsync(alertId);

            if (alert == null)
            {
                _logger.LogWarning(
                    "MarkAsIgnored failed — alert not found | AlertId:{AlertId}",
                    alertId);
                return false;
            }

            // Update alert status
            alert.Status = "Ignored";
            alert.ReviewedAt = DateTime.UtcNow;

            await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Alert marked as ignored | AlertId:{AlertId}",
                alertId);

            return true;
        }

        /// <summary>
        /// Detects multiple failed login attempts from the same user
        /// </summary>
        private async Task DetectMultipleFailedLoginsAsync(
            IEnumerable<AuditLog> recentLogs,
            DateTime since)
        {
            // Read threshold from configuration
            var threshold = _configuration.GetValue<int>(
                "SecurityAlerts:FailedLoginsThreshold", 5);

            // ============================================
            // Detection 1 — Same Username multiple failures
            // ============================================
            var failedByUsername = recentLogs
                .Where(l => l.Action == "LoginFailed")
                .GroupBy(l => l.Username)
                .Where(g => g.Count() >= threshold);

            foreach (var group in failedByUsername)
            {
                // Check if alert already exists for this user in this window
                var existingAlert = await _unitOfWork.SecurityAlerts
                    .GetAllAsync(a =>
                        a.AlertType == "MultipleFailedLogins" &&
                        a.Username == group.Key &&
                        a.CreatedAt >= since);

                // Skip if already alerted
                if (existingAlert.Any())
                    continue;

                var alert = new SecurityAlert
                {
                    AlertType = "MultipleFailedLogins",
                    Severity = "High",
                    Username = group.Key,
                    IpAddress = group.Last().IpAddress,
                    OccurrenceCount = group.Count(),
                    Message = $"User '{group.Key}' failed to login " +
                                    $"{group.Count()} times in the last " +
                                    $"{_configuration.GetValue<int>("SecurityAlerts:MonitoringWindowMinutes", 10)} minutes",
                    CreatedAt = DateTime.UtcNow
                };

                // Save alert
                await _unitOfWork.SecurityAlerts.AddAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                // Send Telegram notification
                var sent = await _telegramAlertSender.SendAlertAsync(alert);

                // Update Telegram status
                alert.TelegramSent = sent;
                alert.TelegramError = sent ? null : "Failed to send Telegram notification";

                await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning(
                    "Security alert created | Type:{AlertType} | User:{Username} | Count:{Count}",
                    alert.AlertType, alert.Username, alert.OccurrenceCount);
            }


            // ============================================
            // Detection 2 — Same IP targeting multiple accounts
            // ============================================
            var failedByIp = recentLogs
                .Where(l => l.Action == "LoginFailed")
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= threshold);

            foreach (var group in failedByIp)
            {
                // Check if alert already exists for this IP in this window
                var existingAlert = await _unitOfWork.SecurityAlerts
                    .GetAllAsync(a =>
                        a.AlertType == "SuspiciousIpLogins" &&
                        a.IpAddress == group.Key &&
                        a.CreatedAt >= since);

                // Skip if already alerted
                if (existingAlert.Any())
                    continue;

                // Count unique usernames tried from this IP
                var uniqueUsernames = group.Select(l => l.Username).Distinct().Count();

                // Skip if only one unique username — already covered by MultipleFailedLogins
                if (uniqueUsernames == 1)
                    continue;

                var alert = new SecurityAlert
                {
                    AlertType = "SuspiciousIpLogins",
                    Severity = uniqueUsernames > 3 ? "Critical" : "High",
                    IpAddress = group.Key,
                    OccurrenceCount = group.Count(),
                    Message = $"IP '{group.Key}' failed to login " +
                                    $"{group.Count()} times targeting " +
                                    $"{uniqueUsernames} different accounts",
                    CreatedAt = DateTime.UtcNow
                };

                // Save alert
                await _unitOfWork.SecurityAlerts.AddAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                // Send Telegram notification
                var sent = await _telegramAlertSender.SendAlertAsync(alert);

                // Update Telegram status
                alert.TelegramSent = sent;
                alert.TelegramError = sent ? null : "Failed to send Telegram notification";

                await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning(
                    "Security alert created | Type:{AlertType} | IP:{IpAddress} | Count:{Count} | UniqueUsers:{UniqueUsers}",
                    alert.AlertType, alert.IpAddress, alert.OccurrenceCount, uniqueUsernames);
            }
        }

        /// <summary>
        /// Detects repeated unauthorized access attempts (403 responses)
        /// </summary>
        private async Task DetectUnauthorizedAccessAsync(
            IEnumerable<AuditLog> recentLogs,
            DateTime since)
        {
            // Read threshold from configuration
            var threshold = _configuration.GetValue<int>(
                "SecurityAlerts:UnauthorizedAttemptsThreshold", 10);

            // Filter: failed actions that are not login attempts
            var unauthorizedAttempts = recentLogs
                .Where(l => !l.Success && l.Action != "LoginFailed")
                .GroupBy(l => l.Username)
                .Where(g => g.Count() >= threshold);

            foreach (var group in unauthorizedAttempts)
            {
                // Check if alert already exists for this user in this window
                var existingAlert = await _unitOfWork.SecurityAlerts
                    .GetAllAsync(a =>
                        a.AlertType == "UnauthorizedAccess" &&
                        a.Username == group.Key &&
                        a.CreatedAt >= since);

                // Skip if already alerted
                if (existingAlert.Any())
                    continue;

                // Count unique actions attempted
                var uniqueActions = group.Select(l => l.Action).Distinct().ToList();

                var alert = new SecurityAlert
                {
                    AlertType = "UnauthorizedAccess",
                    Severity = "High",
                    Username = group.Key,
                    IpAddress = group.Last().IpAddress,
                    OccurrenceCount = group.Count(),
                    Message = $"User '{group.Key}' made {group.Count()} " +
                                    $"unauthorized attempts on: " +
                                    $"{string.Join(", ", uniqueActions)}",
                    CreatedAt = DateTime.UtcNow
                };

                // Save alert
                await _unitOfWork.SecurityAlerts.AddAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                // Send Telegram notification
                var sent = await _telegramAlertSender.SendAlertAsync(alert);

                // Update Telegram status
                alert.TelegramSent = sent;
                alert.TelegramError = sent ? null : "Failed to send Telegram notification";

                await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning(
                    "Security alert created | Type:{AlertType} | User:{Username} | Actions:{Actions}",
                    alert.AlertType, alert.Username, string.Join(", ", uniqueActions));
            }
        }

        /// <summary>
        /// Detects abnormally high activity from a single IP address
        /// </summary>
        private async Task DetectSuspiciousIpActivityAsync(
            IEnumerable<AuditLog> recentLogs,
            DateTime since)
        {
            // Read threshold from configuration
            var threshold = _configuration.GetValue<int>(
                "SecurityAlerts:SuspiciousIpThreshold", 50);

            // Filter: group all activity by IP regardless of success
            var ipActivity = recentLogs
                .Where(l => !string.IsNullOrEmpty(l.IpAddress))
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() >= threshold);

            foreach (var group in ipActivity)
            {
                // Check if alert already exists for this IP in this window
                var existingAlert = await _unitOfWork.SecurityAlerts
                    .GetAllAsync(a =>
                        a.AlertType == "SuspiciousIpActivity" &&
                        a.IpAddress == group.Key &&
                        a.CreatedAt >= since);

                // Skip if already alerted
                if (existingAlert.Any())
                    continue;

                // Analyze activity pattern
                var uniqueUsers = group.Select(l => l.Username).Distinct().Count();
                var uniqueActions = group.Select(l => l.Action).Distinct().ToList();
                var failedCount = group.Count(l => !l.Success);
                var successCount = group.Count(l => l.Success);

                // Determine severity based on pattern
                var severity = uniqueUsers > 5 ? "Critical" : "High";

                var alert = new SecurityAlert
                {
                    AlertType = "SuspiciousIpActivity",
                    Severity = severity,
                    IpAddress = group.Key,
                    OccurrenceCount = group.Count(),
                    Message = $"IP '{group.Key}' made {group.Count()} requests " +
                                    $"in the last {_configuration.GetValue<int>("SecurityAlerts:MonitoringWindowMinutes", 10)} minutes | " +
                                    $"Users:{uniqueUsers} | " +
                                    $"Success:{successCount} | " +
                                    $"Failed:{failedCount} | " +
                                    $"Actions: {string.Join(", ", uniqueActions)}",
                    CreatedAt = DateTime.UtcNow
                };

                // Save alert
                await _unitOfWork.SecurityAlerts.AddAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                // Send Telegram notification
                var sent = await _telegramAlertSender.SendAlertAsync(alert);

                // Update Telegram status
                alert.TelegramSent = sent;
                alert.TelegramError = sent ? null : "Failed to send Telegram notification";

                await _unitOfWork.SecurityAlerts.UpdateAsync(alert);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning(
                    "Security alert created | Type:{AlertType} | IP:{IpAddress} | Requests:{Count} | Users:{Users}",
                    alert.AlertType, alert.IpAddress, alert.OccurrenceCount, uniqueUsers);
            }
        }
    }
}