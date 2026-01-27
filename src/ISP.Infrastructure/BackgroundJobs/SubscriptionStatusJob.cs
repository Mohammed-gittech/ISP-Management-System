using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background Job لتحديث حالات الاشتراكات تلقائياً
    /// </summary>
    public class SubscriptionStatusJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SubscriptionStatusJob> _logger;

        public SubscriptionStatusJob(
            IUnitOfWork unitOfWork,
            ILogger<SubscriptionStatusJob> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ============================================
        // Job: تحديث حالات الاشتراكات
        // Cron: 0 2 * * * (Daily at 2 AM)
        // ============================================

        /// <summary>
        /// تحديث حالة كل الاشتراكات بناءً على تاريخ الانتهاء
        /// Active → Expiring → Expired
        /// </summary>
        public async Task UpdateSubscriptionStatusesAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Update Subscription Statuses Job ===");

                // الحصول على جميع الاشتراكات
                var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

                _logger.LogInformation("Processing {Count} subscriptions", allSubscriptions.Count());

                int updated = 0;
                int noChange = 0;

                foreach (var subscription in allSubscriptions)
                {
                    try
                    {
                        // حفظ الحالة القديمة
                        var oldStatus = subscription.Status;

                        // تحديث الحالة (Method موجود في Entity)
                        subscription.UpdateStatus();

                        // إذا تغيرت الحالة، احفظها
                        if (oldStatus != subscription.Status)
                        {
                            await _unitOfWork.Subscriptions.UpdateAsync(subscription);
                            updated++;

                            _logger.LogInformation(
                                "Subscription {SubscriptionId} status changed: {OldStatus} → {NewStatus}",
                                subscription.Id, oldStatus, subscription.Status);
                        }
                        else
                        {
                            noChange++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error updating status for Subscription {SubscriptionId}",
                            subscription.Id);
                    }
                }

                // حفظ كل التغييرات
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "=== Update Subscription Statuses Job Completed === Updated: {Updated}, No Change: {NoChange}",
                    updated, noChange);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateSubscriptionStatusesAsync job");
            }
        }

        // ============================================
        // Job إضافي: تنظيف البيانات القديمة (Optional)
        // ============================================

        /// <summary>
        /// حذف الإشعارات القديمة (أكثر من 90 يوم)
        /// يُستخدم لتنظيف Database
        /// </summary>
        public async Task CleanupOldNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Cleanup Old Notifications Job ===");

                var allNotifications = await _unitOfWork.Notifications.GetAllAsync();

                var cutoffDate = DateTime.UtcNow.AddDays(-90);

                var oldNotifications = allNotifications
                    .Where(n => n.SentDate.HasValue && n.SentDate.Value < cutoffDate)
                    .ToList();

                _logger.LogInformation("Found {Count} old notifications to delete", oldNotifications.Count);

                foreach (var notification in oldNotifications)
                {
                    await _unitOfWork.Notifications.DeleteAsync(notification);
                }

                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "=== Cleanup Old Notifications Job Completed === Deleted: {Deleted}",
                    oldNotifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CleanupOldNotificationsAsync job");
            }
        }

        // ============================================
        // Job إضافي: إحصائيات يومية (Optional)
        // ============================================

        /// <summary>
        /// حساب وحفظ إحصائيات يومية
        /// </summary>
        public async Task GenerateDailyStatisticsAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Generate Daily Statistics Job ===");

                var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();
                var allSubscribers = await _unitOfWork.Subscribers.GetAllAsync();
                var allTenants = await _unitOfWork.Tenants.GetAllAsync();

                // إحصائيات لكل Tenant
                foreach (var tenant in allTenants)
                {
                    var tenantSubscriptions = allSubscriptions
                        .Where(s => s.TenantId == tenant.Id)
                        .ToList();

                    var tenantSubscribers = allSubscribers
                        .Where(s => s.TenantId == tenant.Id)
                        .ToList();

                    var activeSubscriptions = tenantSubscriptions
                        .Count(s => s.Status == Domain.Enums.SubscriptionStatus.Active);

                    var expiringSubscriptions = tenantSubscriptions
                        .Count(s => s.Status == Domain.Enums.SubscriptionStatus.Expiring);

                    var expiredSubscriptions = tenantSubscriptions
                        .Count(s => s.Status == Domain.Enums.SubscriptionStatus.Expired);

                    _logger.LogInformation(
                        "Tenant {TenantName} (ID: {TenantId}) Stats: " +
                        "Subscribers={Subscribers}, Active={Active}, Expiring={Expiring}, Expired={Expired}",
                        tenant.Name, tenant.Id,
                        tenantSubscribers.Count,
                        activeSubscriptions,
                        expiringSubscriptions,
                        expiredSubscriptions
                    );

                    // TODO: حفظ الإحصائيات في جدول DailyStatistics (Phase 3)
                }

                _logger.LogInformation("=== Generate Daily Statistics Job Completed ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateDailyStatisticsAsync job");
            }
        }
    }
}