using ISP.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background Job للحذف النهائي للبيانات المحذوفة القديمة
    /// ✅ يعمل تلقائياً كل يوم
    /// ✅ Configurable Retention Period
    /// ✅ يحذف من كل الجداول (Subscribers, Plans, Subscriptions, Users)
    /// </summary>
    public class RetentionCleanupJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RetentionCleanupJob> _logger;

        public RetentionCleanupJob(
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ILogger<RetentionCleanupJob> logger)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// تنفيذ Cleanup Job
        /// </summary>
        public async Task ExecuteAsync()
        {
            _logger.LogInformation("=== Starting Retention Cleanup Job ===");

            try
            {
                // 1. قراءة Retention Period من Configuration
                var retentionDays = _configuration.GetValue<int>("SoftDelete:RetentionDays", 90);
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                _logger.LogInformation("Retention Period: {Days} days | Cutoff Date: {Date}",
                    retentionDays, cutoffDate.ToString("yyyy-MM-dd"));

                // 2. حذف Subscribers القديمة
                var subscribersDeleted = await CleanupSubscribersAsync(cutoffDate);

                // 3. حذف Plans القديمة
                var plansDeleted = await CleanupPlansAsync(cutoffDate);

                // 4. حذف Subscriptions القديمة
                var subscriptionsDeleted = await CleanupSubscriptionsAsync(cutoffDate);

                // 5. حذف Users القدامى (اختياري - بحذر!)
                var usersDeleted = 0;
                var enableUserCleanup = _configuration.GetValue<bool>("SoftDelete:CleanupUsers", false);

                if (enableUserCleanup)
                {
                    usersDeleted = await CleanupUsersAsync(cutoffDate);
                }
                else
                {
                    _logger.LogInformation("User cleanup is disabled in configuration");
                }

                // 6. تقرير نهائي
                _logger.LogInformation("=== Retention Cleanup Completed ===");
                _logger.LogInformation("Deleted: {Subscribers} Subscribers, {Plans} Plans, {Subscriptions} Subscriptions, {Users} Users",
                    subscribersDeleted, plansDeleted, subscriptionsDeleted, usersDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Retention Cleanup Job");
                throw;
            }
        }

        // ============================================
        // CLEANUP METHODS
        // ============================================

        /// <summary>
        /// حذف Subscribers المحذوفة قبل تاريخ معين
        /// </summary>
        private async Task<int> CleanupSubscribersAsync(DateTime cutoffDate)
        {
            _logger.LogInformation("Cleaning up Subscribers deleted before {Date}", cutoffDate);

            try
            {
                var count = await _unitOfWork.Subscribers.PermanentDeleteOldAsync(cutoffDate);

                if (count > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogWarning("Permanently deleted {Count} Subscribers", count);
                }
                else
                {
                    _logger.LogInformation("No old Subscribers to delete");
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Subscribers");
                return 0;
            }
        }

        /// <summary>
        /// حذف Plans المحذوفة قبل تاريخ معين
        /// </summary>
        private async Task<int> CleanupPlansAsync(DateTime cutoffDate)
        {
            _logger.LogInformation("Cleaning up Plans deleted before {Date}", cutoffDate);

            try
            {
                var count = await _unitOfWork.Plans.PermanentDeleteOldAsync(cutoffDate);

                if (count > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogWarning("Permanently deleted {Count} Plans", count);
                }
                else
                {
                    _logger.LogInformation("No old Plans to delete");
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Plans");
                return 0;
            }
        }

        /// <summary>
        /// حذف Subscriptions المحذوفة قبل تاريخ معين
        /// </summary>
        private async Task<int> CleanupSubscriptionsAsync(DateTime cutoffDate)
        {
            _logger.LogInformation("Cleaning up Subscriptions deleted before {Date}", cutoffDate);

            try
            {
                var count = await _unitOfWork.Subscriptions.PermanentDeleteOldAsync(cutoffDate);

                if (count > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogWarning("Permanently deleted {Count} Subscriptions", count);
                }
                else
                {
                    _logger.LogInformation("No old Subscriptions to delete");
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Subscriptions");
                return 0;
            }
        }

        /// <summary>
        /// حذف Users المحذوفين قبل تاريخ معين
        /// ⚠️ يُستخدم بحذر - معطل افتراضياً
        /// </summary>
        private async Task<int> CleanupUsersAsync(DateTime cutoffDate)
        {
            _logger.LogWarning("Cleaning up Users deleted before {Date}", cutoffDate);

            try
            {
                var count = await _unitOfWork.Users.PermanentDeleteOldAsync(cutoffDate);

                if (count > 0)
                {
                    await _unitOfWork.SaveChangesAsync();
                    _logger.LogCritical("Permanently deleted {Count} Users", count);
                }
                else
                {
                    _logger.LogInformation("No old Users to delete");
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up Users");
                return 0;
            }
        }
    }
}