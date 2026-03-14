using ISP.Application.DTOs.SecurityAlerts;
using ISP.Domain.Entities;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// Contract for detecting and managing security alerts
    /// </summary>
    public interface ISecurityAlertService
    {
        /// <summary>
        /// Analyzes recent audit logs and creates alerts for suspicious activity
        /// </summary>
        Task DetectAndAlertAsync();

        /// <summary>
        /// Returns all security alerts with optional status filter
        /// </summary>
        Task<IEnumerable<SecurityAlertDto>> GetAlertsAsync(string? status = null);

        /// <summary>
        /// Marks an alert as reviewed with optional notes
        /// </summary>
        Task<bool> MarkAsReviewedAsync(int alertId, string? notes = null);

        /// <summary>
        /// Marks an alert as resolved
        /// </summary>
        Task<bool> MarkAsResolvedAsync(int alertId);

        /// <summary>
        /// Marks an alert as ignored (false positive)
        /// </summary>
        Task<bool> MarkAsIgnoredAsync(int alertId);
    }
}