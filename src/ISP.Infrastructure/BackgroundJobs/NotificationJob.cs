using ISP.Application.Interfaces;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Background Job لإرسال الإشعارات التلقائية
    /// </summary>
    public class NotificationJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITelegramService _telegramService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationJob> _logger;

        public NotificationJob(
            IUnitOfWork unitOfWork,
            ITelegramService telegramService,
            INotificationService notificationService,
            ILogger<NotificationJob> logger)
        {
            _unitOfWork = unitOfWork;
            _telegramService = telegramService;
            _notificationService = notificationService;
            _logger = logger;
        }

        // ============================================
        // Job 1: إرسال تنبيهات انتهاء الاشتراك
        // Cron: 0 1 * * * (Daily at 1 AM)
        // ============================================

        /// <summary>
        /// فحص الاشتراكات وإرسال تنبيهات قبل الانتهاء
        /// يرسل تنبيهات في: 7، 3، 1 يوم قبل الانتهاء
        /// </summary>
        public async Task SendExpiryNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Expiry Notifications Job ===");

                // الأيام التي نرسل فيها تنبيهات
                int[] warningDays = { 7, 3, 1 };

                int totalSent = 0;
                int totalFailed = 0;

                foreach (var days in warningDays)
                {
                    _logger.LogInformation("Checking subscriptions expiring in {Days} days...", days);

                    var (sent, failed) = await SendExpiryWarningsForDaysAsync(days);

                    totalSent += sent;
                    totalFailed += failed;
                }

                _logger.LogInformation(
                    "=== Expiry Notifications Job Completed === Sent: {Sent}, Failed: {Failed}",
                    totalSent, totalFailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendExpiryNotificationsAsync job");
            }
        }

        /// <summary>
        /// إرسال تنبيهات للاشتراكات التي تنتهي خلال عدد أيام محدد
        /// </summary>
        private async Task<(int sent, int failed)> SendExpiryWarningsForDaysAsync(int daysRemaining)
        {
            int sent = 0;
            int failed = 0;

            try
            {
                // 1. حساب التاريخ المستهدف
                var targetDate = DateTime.UtcNow.Date.AddDays(daysRemaining);

                // 2. الحصول على كل الاشتراكات
                var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

                // 3. فلترة الاشتراكات التي تنتهي في التاريخ المستهدف
                var expiringSubscriptions = allSubscriptions
                    .Where(s => s.EndDate.Date == targetDate)
                    .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expiring)
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} subscriptions expiring on {Date} ({Days} days)",
                    expiringSubscriptions.Count, targetDate, daysRemaining);

                // 4. لكل اشتراك، إرسال تنبيه
                foreach (var subscription in expiringSubscriptions)
                {
                    try
                    {
                        // Load Navigation Properties
                        await LoadSubscriptionRelationsAsync(subscription);

                        // التحقق من وجود ChatId
                        if (string.IsNullOrWhiteSpace(subscription.Subscriber.TelegramChatId))
                        {
                            _logger.LogWarning(
                                "Subscriber {SubscriberId} has no Telegram ChatId, skipping",
                                subscription.SubscriberId);
                            failed++;
                            continue;
                        }

                        // التحقق من عدم إرسال تنبيه مسبقاً لنفس اليوم
                        var alreadySent = await CheckIfNotificationAlreadySentAsync(
                            subscription.SubscriberId,
                            NotificationType.ExpiryWarning,
                            daysRemaining
                        );

                        if (alreadySent)
                        {
                            _logger.LogDebug(
                                "Notification already sent for Subscriber {SubscriberId} ({Days} days warning)",
                                subscription.SubscriberId, daysRemaining);
                            continue;
                        }

                        // إرسال التنبيه
                        bool success = await _telegramService.SendExpiryWarningAsync(
                            subscription.TenantId,
                            subscription,
                            daysRemaining
                        );

                        if (success)
                        {
                            // حفظ الإشعار في Database
                            await _notificationService.CreateNotificationAsync(
                                subscription.TenantId,
                                subscription.SubscriberId,
                                NotificationType.ExpiryWarning,
                                $"Expiry warning: {daysRemaining} days remaining",
                                NotificationChannel.Telegram
                            );

                            sent++;
                            _logger.LogInformation(
                                "Expiry warning sent: Subscriber={SubscriberName}, Days={Days}",
                                subscription.Subscriber.FullName, daysRemaining);
                        }
                        else
                        {
                            failed++;
                            _logger.LogWarning(
                                "Failed to send expiry warning: Subscriber={SubscriberId}",
                                subscription.SubscriberId);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex,
                            "Error sending expiry warning for Subscription {SubscriptionId}",
                            subscription.Id);
                    }

                    // تأخير بسيط لتجنب Rate Limiting
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendExpiryWarningsForDaysAsync");
            }

            return (sent, failed);
        }

        // ============================================
        // Job 2: إعادة محاولة الإشعارات الفاشلة
        // Cron: 0 */6 * * * (Every 6 hours)
        // ============================================

        /// <summary>
        /// إعادة محاولة إرسال الإشعارات الفاشلة
        /// </summary>
        public async Task RetryFailedNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Retry Failed Notifications Job ===");

                // الحصول على الإشعارات الفاشلة
                var failedNotifications = await _notificationService.GetFailedNotificationsAsync(
                    tenantId: null, // كل الوكلاء
                    maxRetries: 3
                );

                var notificationsList = failedNotifications.ToList();

                _logger.LogInformation("Found {Count} failed notifications to retry", notificationsList.Count);

                int retried = 0;
                int successRetries = 0;

                foreach (var notification in notificationsList)
                {
                    try
                    {
                        _logger.LogInformation("Retrying notification {NotificationId}", notification.Id);

                        bool success = await _notificationService.RetryFailedNotificationAsync(notification.Id);

                        retried++;

                        if (success)
                        {
                            successRetries++;
                            _logger.LogInformation(
                                "Successfully retried notification {NotificationId}",
                                notification.Id);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Retry failed for notification {NotificationId}",
                                notification.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error retrying notification {NotificationId}",
                            notification.Id);
                    }

                    // تأخير بين المحاولات
                    await Task.Delay(200);
                }

                _logger.LogInformation(
                    "=== Retry Failed Notifications Job Completed === Retried: {Retried}, Success: {Success}",
                    retried, successRetries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RetryFailedNotificationsAsync job");
            }
        }

        // ============================================
        // Job 3: إرسال تنبيهات الاشتراكات المنتهية
        // Cron: 0 3 * * * (Daily at 3 AM)
        // ============================================

        /// <summary>
        /// إرسال تنبيهات للاشتراكات المنتهية (بعد انتهاء الاشتراك)
        /// </summary>
        public async Task SendExpiredNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("=== Starting Expired Notifications Job ===");

                // الحصول على الاشتراكات المنتهية اليوم
                var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

                var expiredToday = allSubscriptions
                    .Where(s => s.EndDate.Date == DateTime.UtcNow.Date.AddDays(-1)) // منتهية أمس
                    .Where(s => s.Status == SubscriptionStatus.Expired)
                    .ToList();

                _logger.LogInformation("Found {Count} subscriptions expired yesterday", expiredToday.Count);

                int sent = 0;
                int failed = 0;

                foreach (var subscription in expiredToday)
                {
                    try
                    {
                        await LoadSubscriptionRelationsAsync(subscription);

                        if (string.IsNullOrWhiteSpace(subscription.Subscriber.TelegramChatId))
                        {
                            failed++;
                            continue;
                        }

                        bool success = await _telegramService.SendExpiredNotificationAsync(
                            subscription.TenantId,
                            subscription
                        );

                        if (success)
                        {
                            await _notificationService.CreateNotificationAsync(
                                subscription.TenantId,
                                subscription.SubscriberId,
                                NotificationType.ExpiryWarning,
                                "Subscription expired",
                                NotificationChannel.Telegram
                            );

                            sent++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex,
                            "Error sending expired notification for Subscription {SubscriptionId}",
                            subscription.Id);
                    }

                    await Task.Delay(100);
                }

                _logger.LogInformation(
                    "=== Expired Notifications Job Completed === Sent: {Sent}, Failed: {Failed}",
                    sent, failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendExpiredNotificationsAsync job");
            }
        }

        // ============================================
        // Helper Methods
        // ============================================

        /// <summary>
        /// تحميل Relations للـ Subscription
        /// </summary>
        private async Task LoadSubscriptionRelationsAsync(Domain.Entities.Subscription subscription)
        {
            subscription.Subscriber = (await _unitOfWork.Subscribers.GetByIdAsync(subscription.SubscriberId))!;
            subscription.Plan = (await _unitOfWork.Plans.GetByIdAsync(subscription.PlanId))!;
            subscription.Tenant = (await _unitOfWork.Tenants.GetByIdAsync(subscription.TenantId))!;
        }

        /// <summary>
        /// التحقق من عدم إرسال إشعار مكرر لنفس اليوم
        /// </summary>
        private async Task<bool> CheckIfNotificationAlreadySentAsync(
            int subscriberId,
            NotificationType type,
            int daysRemaining)
        {
            try
            {
                var latest = await _notificationService.GetLatestForSubscriberAsync(subscriberId, type);

                if (latest == null)
                    return false;

                // إذا تم الإرسال اليوم، لا نرسل مرة ثانية
                if (latest.SentDate.HasValue && latest.SentDate.Value.Date == DateTime.UtcNow.Date)
                    return true;

                // التحقق من الرسالة (تحتوي على عدد الأيام؟)
                if (latest.Message.Contains($"{daysRemaining}") && latest.Status == NotificationStatus.Sent)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}