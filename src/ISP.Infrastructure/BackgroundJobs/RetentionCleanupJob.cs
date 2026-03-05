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

                // 6. Refresh Tokens cleanup (if applicable)
                var refreshTokensDeleted = await CleanupRefreshTokensAsync();

                // 7. تقرير نهائي
                _logger.LogInformation("=== Retention Cleanup Completed ===");
                _logger.LogInformation(
                    "Deleted: {Subscribers} Subscribers, {Plans} Plans, " +
                    "{Subscriptions} Subscriptions, {Users} Users, " +
                    "{RefreshTokens} RefreshTokens",
                    subscribersDeleted, plansDeleted,
                    subscriptionsDeleted, usersDeleted,
                    refreshTokensDeleted);
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

        // ============================================
        // Cleanup RefreshTokens ← جديد
        // ============================================

        /// <summary>
        /// حذف RefreshTokens المنتهية والمُلغاة القديمة
        ///
        /// يحذف توكناً إذا تحقق أحد الشرطين:
        ///
        ///   الشرط 1 — منتهٍ منذ أكثر من 90 يوم:
        ///     ExpiresAt < (UtcNow - 90 يوم)
        ///     مثال: لو اليوم 15 مارس، يُحذف كل توكن انتهى قبل 14 ديسمبر
        ///
        ///   الشرط 2 — مُلغى منذ أكثر من 90 يوم:
        ///     IsRevoked = true AND RevokedAt < (UtcNow - 90 يوم)
        ///     مثال: كل توكن أُلغي قبل 14 ديسمبر يُحذف الآن
        ///
        /// لماذا 90 يوم للاثنين؟
        ///   نافذة تحقيق كافية لو اكتشفنا اختراقاً بعد شهرين
        ///   يبقى التوكن في DB ونعرف من استخدمه ومتى
        ///   بعد 90 يوم لا قيمة أمنية له
        /// </summary>
        private async Task<int> CleanupRefreshTokensAsync()
        {
            _logger.LogInformation("Cleaning up expired and revoked RefreshTokens...");

            try
            {
                // قراءة Retention من Configuration
                // القيمة الافتراضية 90 يوم للاثنين
                var expiredRetentionDays = _configuration
                    .GetValue<int>("RefreshTokens:ExpiredRetentionDays", 90);
                // كم يوم نحتفظ بالتوكن بعد انتهاء صلاحيته

                var revokedRetentionDays = _configuration
                    .GetValue<int>("RefreshTokens:RevokedRetentionDays", 90);
                // كم يوم نحتفظ بالتوكن بعد إلغائه

                var expiredCutoffDate = DateTime.UtcNow.AddDays(-expiredRetentionDays);
                // التوكنات التي انتهت قبل هذا التاريخ → تُحذف

                var revokedCutoffDate = DateTime.UtcNow.AddDays(-revokedRetentionDays);
                // التوكنات التي أُلغيت قبل هذا التاريخ → تُحذف

                _logger.LogInformation(
                    "RefreshToken Cleanup | " +
                    "Expired cutoff: {ExpiredDate} | " +
                    "Revoked cutoff: {RevokedDate}",
                    expiredCutoffDate.ToString("yyyy-MM-dd"),
                    revokedCutoffDate.ToString("yyyy-MM-dd"));

                // جلب كل الـ RefreshTokens بما فيها المُلغاة
                var allTokens = await _unitOfWork.RefreshTokens
                    .GetAllIncludingDeletedAsync();

                // فلترة التوكنات المراد حذفها
                var tokensToDelete = allTokens
                    .Where(t =>
                        (!t.IsRevoked && t.ExpiresAt < expiredCutoffDate)
                        ||
                        (t.IsRevoked && t.RevokedAt.HasValue && t.RevokedAt.Value < revokedCutoffDate)
                    )
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} RefreshTokens to delete",
                    tokensToDelete.Count);

                if (tokensToDelete.Count == 0)
                {
                    _logger.LogInformation("No RefreshTokens to clean up");
                    return 0;
                }

                // حذف نهائي لكل توكن
                foreach (var token in tokensToDelete)
                {
                    await _unitOfWork.RefreshTokens.DeleteAsync(token);
                }


                await _unitOfWork.SaveChangesAsync();

                _logger.LogWarning(
                    "Permanently deleted {Count} RefreshTokens",
                    tokensToDelete.Count);

                return tokensToDelete.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up RefreshTokens");
                return 0;
            }
        }
    }
}