using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services.Notifications
{
    /// <summary>
    /// خدمة إدارة الإشعارات
    /// تتعامل مع إنشاء وحفظ وإرسال الإشعارات عبر قنوات مختلفة
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITelegramService _telegramService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
        IUnitOfWork unitOfWork,
        ITelegramService telegramService,
        ILogger<NotificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _telegramService = telegramService;
            _logger = logger;
        }

        // ============================================
        // Create Notification
        // ============================================

        /// <summary>
        /// إنشاء وحفظ إشعار جديد في Database (Status: Pending)
        /// </summary>
        public async Task<Notification> CreateNotificationAsync(
            int tenantId,
            int subscriberId,
            NotificationType type,
            string message,
            NotificationChannel channel = NotificationChannel.Telegram)
        {
            try
            {


                // 1. التحقق من وجود Subscriber
                var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(subscriberId);
                if (subscriber == null)
                {
                    throw new InvalidOperationException($"Subscriber with ID {subscriberId} not found");
                }

                // 2. التحقق من TenantId Match
                if (subscriber.TenantId != tenantId)
                {
                    throw new InvalidOperationException(
                        $"Subscriber {subscriberId} does not belong to Tenant {tenantId}");
                }

                // 3. إنشاء Notification Entity
                var notification = new Notification
                {
                    TenantId = tenantId,
                    SubscriberId = subscriberId,
                    Type = type,
                    Message = message,
                    Channel = channel,
                    Status = NotificationStatus.Pending,
                    SentDate = null,
                    ErrorMessage = null,
                    TelegramMessageId = null
                };

                // 4. حفظ في Database
                await _unitOfWork.Notifications.AddAsync(notification);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Notification created: ID={NotificationId}, Type={Type}, Subscriber={SubscriberId}, Tenant={TenantId}",
                    notification.Id, type, subscriberId, tenantId);

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create notification: Type={Type}, Subscriber={SubscriberId}, Tenant={TenantId}",
                    type, subscriberId, tenantId);
                throw;
            }
        }

        // ============================================
        // Send Notification
        // ============================================

        /// <summary>
        /// إرسال إشعار موجود مسبقاً في Database
        /// يحدث Status بناءً على نتيجة الإرسال
        /// </summary>
        public async Task<bool> SendNotificationAsync(int notificationId)
        {
            try
            {
                // 1. الحصول على Notification مع Subscriber و Tenant
                var notification = await GetByIdAsync(notificationId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification with ID {NotificationId} not found", notificationId);
                    return false;
                }

                // 2. التحقق من الحالة (لا نرسل إذا كان Sent بالفعل)
                if (notification.Status == NotificationStatus.Sent)
                {
                    _logger.LogWarning(
                        "Notification {NotificationId} already sent on {SentDate}",
                        notificationId, notification.SentDate);
                    return true; // نرجع true لأنه مُرسل بالفعل
                }

                // 3. إرسال حسب القناة
                bool success = false;
                switch (notification.Channel)
                {
                    case NotificationChannel.Telegram:
                        success = await SendViaTelegramAsync(notification);
                        break;

                    case NotificationChannel.WhatsApp:
                        // TODO: Phase 3 - WhatsApp Integration
                        _logger.LogWarning("WhatsApp channel not implemented yet");
                        notification.ErrorMessage = "WhatsApp integration not available";
                        break;

                    case NotificationChannel.Email:
                        // TODO: Phase 3 - Email Integration
                        _logger.LogWarning("Email channel not implemented yet");
                        notification.ErrorMessage = "Email integration not available";
                        break;

                    case NotificationChannel.SMS:
                        // TODO: Phase 3 - SMS Integration
                        _logger.LogWarning("SMS channel not implemented yet");
                        notification.ErrorMessage = "SMS integration not available";
                        break;

                    default:
                        _logger.LogError("Unknown notification channel: {Channel}", notification.Channel);
                        notification.ErrorMessage = $"Unknown channel: {notification.Channel}";
                        break;
                }

                // 4. تحديث Status في Database
                if (success)
                {
                    notification.Status = NotificationStatus.Sent;
                    notification.SentDate = DateTime.UtcNow;
                    notification.ErrorMessage = null;

                    _logger.LogInformation(
                        "Notification {NotificationId} sent successfully via {Channel}",
                        notificationId, notification.Channel);
                }
                else
                {
                    notification.Status = NotificationStatus.Failed;
                    // ErrorMessage تم تعيينه في SendViaTelegramAsync

                    _logger.LogWarning(
                        "Notification {NotificationId} failed to send via {Channel}: {Error}",
                        notificationId, notification.Channel, notification.ErrorMessage);
                }

                // 5. حفظ التحديثات
                await _unitOfWork.Notifications.UpdateAsync(notification);
                await _unitOfWork.SaveChangesAsync();

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification {NotificationId}", notificationId);

                // محاولة حفظ الخطأ في Database
                try
                {
                    var notification = await GetByIdAsync(notificationId);
                    if (notification != null)
                    {
                        notification.Status = NotificationStatus.Failed;
                        notification.ErrorMessage = ex.Message;
                        await _unitOfWork.Notifications.UpdateAsync(notification);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to update notification status after error");
                }

                return false;
            }
        }

        /// <summary>
        /// إرسال إشعار عبر Telegram
        /// </summary>
        private async Task<bool> SendViaTelegramAsync(Notification notification)
        {
            try
            {
                // 1. التحقق من وجود ChatId
                if (string.IsNullOrWhiteSpace(notification.Subscriber.TelegramChatId))
                {
                    notification.ErrorMessage = "Subscriber has no Telegram ChatId";
                    _logger.LogWarning(
                        "Subscriber {SubscriberId} has no Telegram ChatId",
                        notification.SubscriberId);
                    return false;
                }

                // 2. إرسال عبر TelegramService
                bool success = await _telegramService.SendMessageAsync(
                    notification.TenantId,
                    notification.Subscriber.TelegramChatId,
                    notification.Message
                );

                if (!success)
                {
                    notification.ErrorMessage = "Failed to send via Telegram (check TelegramService logs)";
                }

                return success;
            }
            catch (Exception ex)
            {
                notification.ErrorMessage = $"Telegram error: {ex.Message}";
                _logger.LogError(ex, "Error sending notification via Telegram");
                return false;
            }
        }

        // ============================================
        // Create and Send (One Step)
        // ============================================

        /// <summary>
        /// إنشاء وإرسال إشعار في خطوة واحدة
        /// </summary>
        public async Task<bool> CreateAndSendAsync(
            int tenantId,
            int subscriberId,
            NotificationType type,
            string message,
            NotificationChannel channel = NotificationChannel.Telegram)
        {
            try
            {
                // 1. إنشاء الإشعار
                var notification = await CreateNotificationAsync(
                    tenantId,
                    subscriberId,
                    type,
                    message,
                    channel
                );

                // 2. إرسال الإشعار
                bool success = await SendNotificationAsync(notification.Id);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create and send notification: Type={Type}, Subscriber={SubscriberId}",
                    type, subscriberId);
                return false;
            }
        }

        // ============================================
        // Retry Failed Notifications
        // ============================================

        /// <summary>
        /// إعادة محاولة إرسال إشعار فاشل
        /// </summary>
        public async Task<bool> RetryFailedNotificationAsync(int notificationId)
        {
            try
            {
                var notification = await GetByIdAsync(notificationId);

                if (notification == null)
                {
                    _logger.LogWarning("Notification {NotificationId} not found for retry", notificationId);
                    return false;
                }

                if (notification.Status != NotificationStatus.Failed)
                {
                    _logger.LogWarning(
                        "Notification {NotificationId} is not in Failed status (current: {Status})",
                        notificationId, notification.Status);
                    return false;
                }

                _logger.LogInformation("Retrying notification {NotificationId}", notificationId);

                // إعادة تعيين الحالة لـ Pending
                notification.Status = NotificationStatus.Pending;
                notification.ErrorMessage = null;
                await _unitOfWork.Notifications.UpdateAsync(notification);
                await _unitOfWork.SaveChangesAsync();

                // محاولة الإرسال
                return await SendNotificationAsync(notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying notification {NotificationId}", notificationId);
                return false;
            }
        }

        /// <summary>
        /// الحصول على جميع الإشعارات الفاشلة (للـ Retry Job)
        /// </summary>
        public async Task<IEnumerable<Notification>> GetFailedNotificationsAsync(
            int? tenantId = null,
            int maxRetries = 3)
        {
            try
            {
                var allNotifications = await _unitOfWork.Notifications.GetAllAsync();

                var query = allNotifications
                    .Where(n => n.Status == NotificationStatus.Failed);

                // Filter by TenantId if provided
                if (tenantId.HasValue)
                {
                    query = query.Where(n => n.TenantId == tenantId.Value);
                }

                // TODO: Track retry count per notification
                // For now, we return all failed notifications
                // In Phase 3, add a RetryCount property to Notification entity

                return query.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting failed notifications");
                return Enumerable.Empty<Notification>();
            }
        }

        // ============================================
        // Get Methods
        // ============================================

        /// <summary>
        /// الحصول على إشعار بالـ Id (مع Navigation Properties)
        /// </summary>
        public async Task<Notification?> GetByIdAsync(int notificationId)
        {
            try
            {
                // ملاحظة: GenericRepository لا يدعم Include
                // نحتاج استعلام مخصص من DbContext

                // الحل المؤقت: نجيب من UnitOfWork ثم نجيب الـ Relations يدوياً
                var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);

                if (notification == null)
                    return null;

                // Load Navigation Properties manually
                var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(notification.SubscriberId);
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(notification.TenantId);

                if (subscriber != null)
                    notification.Subscriber = subscriber;

                if (tenant != null)
                    notification.Tenant = tenant;

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification {NotificationId}", notificationId);
                return null;
            }
        }

        /// <summary>
        /// الحصول على آخر إشعار لمشترك معين
        /// </summary>
        public async Task<Notification?> GetLatestForSubscriberAsync(
            int subscriberId,
            NotificationType? type = null)
        {
            try
            {
                var allNotifications = await _unitOfWork.Notifications.GetAllAsync();

                var query = allNotifications
                    .Where(n => n.SubscriberId == subscriberId);

                if (type.HasValue)
                {
                    query = query.Where(n => n.Type == type.Value);
                }

                var latest = query
                    .OrderByDescending(n => n.Id)
                    .FirstOrDefault();

                if (latest != null)
                {
                    // Load Navigation Properties
                    var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(latest.SubscriberId);
                    if (subscriber != null)
                        latest.Subscriber = subscriber;
                }

                return latest;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error getting latest notification for Subscriber {SubscriberId}",
                    subscriberId);
                return null;
            }
        }
    }
}
