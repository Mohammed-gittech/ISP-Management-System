using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ISP.Infrastructure.Services.Telegram
{
    /// <summary>
    /// خدمة Telegram Bot - Multi-Tenant
    /// كل Tenant له Bot Token خاص به
    /// </summary>
    public class TelegramService : ITelegramService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TelegramService> _logger;

        public TelegramService(
            IUnitOfWork unitOfWork,
            ILogger<TelegramService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ============================================
        // Private Helper: إنشاء Bot Client
        // ============================================

        /// <summary>
        /// إنشاء TelegramBotClient خاص بـ Tenant معين
        /// يجلب Bot Token من Database
        /// </summary>
        /// <param name="tenantId">معرف الوكيل</param>
        /// <returns>TelegramBotClient أو null إذا لم يوجد Token</returns>
        private async Task<ITelegramBotClient?> GetBotClientAsync(int tenantId)
        {
            try
            {
                // 1. الحصول على Tenant من Database
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

                // 2. التحقق من وجود Tenant و Token
                if (tenant == null)
                {
                    _logger.LogWarning("Tenant with ID {TenantId} not found", tenantId);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tenant.TelegramBotToken))
                {
                    _logger.LogWarning("Tenant {TenantName} (ID: {TenantId}) has no Telegram Bot Token configured",
                        tenant.Name, tenantId);
                    return null;
                }

                // 3. إنشاء Bot Client
                var botClient = new TelegramBotClient(tenant.TelegramBotToken);

                _logger.LogDebug("Created Telegram Bot Client for Tenant: {TenantName} (ID: {TenantId})",
                tenant.Name, tenantId);

                return botClient;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Telegram Bot Client for Tenant ID: {TenantId}", tenantId);
                return null;
            }
        }

        // ============================================
        // Public Methods (من Interface)
        // ============================================

        /// <summary>
        /// إرسال رسالة نصية بسيطة
        /// </summary>
        public async Task<bool> SendMessageAsync(int tenantId, string chatId, string message)
        {
            try
            {
                // 1. الحصول على Bot Client
                var botClient = await GetBotClientAsync(tenantId);
                if (botClient == null)
                    return false;

                // 2. التحقق من المعاملات
                if (string.IsNullOrWhiteSpace(chatId))
                {
                    _logger.LogWarning("ChatId is empty for Tenant ID: {TenantId}", tenantId);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogWarning("Message is empty for Tenant ID: {TenantId}", tenantId);
                    return false;
                }

                // 3. إرسال الرسالة
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: message,
                    parseMode: ParseMode.Html, // لدعم HTML formatting
                    cancellationToken: default
                );

                _logger.LogInformation(
                    "Message sent successfully to ChatId: {ChatId}, MessageId: {MessageId}, Tenant: {TenantId}",
                    chatId, sentMessage.MessageId, tenantId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send Telegram message to ChatId: {ChatId}, Tenant: {TenantId}",
                    chatId, tenantId);
                return false;
            }
        }

        /// <summary>
        /// إرسال تنبيه قبل انتهاء الاشتراك
        /// </summary>
        public async Task<bool> SendExpiryWarningAsync(int tenantId, Subscription subscription, int daysRemaining)
        {
            try
            {
                // 1. التحقق من وجود ChatId
                if (string.IsNullOrWhiteSpace(subscription.Subscriber.TelegramChatId))
                {
                    _logger.LogWarning(
                        "Subscriber {SubscriberName} (ID: {SubscriberId}) has no Telegram ChatId",
                        subscription.Subscriber.FullName, subscription.SubscriberId);
                    return false;
                }

                // 2. بناء الرسالة
                string message = FormatExpiryWarningMessage(subscription, daysRemaining);

                // 3. إرسال الرسالة
                return await SendMessageAsync(tenantId, subscription.Subscriber.TelegramChatId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send expiry warning for Subscription ID: {SubscriptionId}, Tenant: {TenantId}",
                    subscription.Id, tenantId);
                return false;
            }
        }

        /// <summary>
        /// إرسال تأكيد تجديد الاشتراك
        /// </summary>
        public async Task<bool> SendRenewalConfirmationAsync(int tenantId, Subscription subscription)
        {
            try
            {
                // 1. التحقق من وجود ChatId
                if (string.IsNullOrWhiteSpace(subscription.Subscriber.TelegramChatId))
                {
                    _logger.LogWarning(
                        "Subscriber {SubscriberName} (ID: {SubscriberId}) has no Telegram ChatId",
                        subscription.Subscriber.FullName, subscription.SubscriberId);
                    return false;
                }

                // 2. بناء الرسالة
                string message = FormatRenewalConfirmationMessage(subscription);

                // 3. إرسال الرسالة
                return await SendMessageAsync(tenantId, subscription.Subscriber.TelegramChatId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send renewal confirmation for Subscription ID: {SubscriptionId}, Tenant: {TenantId}",
                    subscription.Id, tenantId);
                return false;
            }
        }

        /// <summary>
        /// إرسال إشعار بانتهاء الاشتراك (بعد الانتهاء الفعلي)
        /// </summary>
        public async Task<bool> SendExpiredNotificationAsync(int tenantId, Subscription subscription)
        {
            try
            {
                // 1. التحقق من وجود ChatId
                if (string.IsNullOrWhiteSpace(subscription.Subscriber.TelegramChatId))
                {
                    _logger.LogWarning(
                        "Subscriber {SubscriberName} (ID: {SubscriberId}) has no Telegram ChatId",
                        subscription.Subscriber.FullName, subscription.SubscriberId);
                    return false;
                }

                // 2. بناء الرسالة
                string message = FormatExpiredNotificationMessage(subscription);

                // 3. إرسال الرسالة
                return await SendMessageAsync(tenantId, subscription.Subscriber.TelegramChatId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send expired notification for Subscription ID: {SubscriptionId}, Tenant: {TenantId}",
                    subscription.Id, tenantId);
                return false;
            }
        }

        /// <summary>
        /// اختبار الاتصال بـ Telegram Bot والتحقق من صحة Token
        /// </summary>
        public async Task<bool> TestConnectionAsync(string botToken)
        {
            try
            {
                // 1. التحقق من Token
                if (string.IsNullOrWhiteSpace(botToken))
                {
                    _logger.LogWarning("Bot Token is empty");
                    return false;
                }

                // 2. إنشاء Bot Client
                var botClient = new TelegramBotClient(botToken);

                // 3. اختبار الاتصال (GetMe)
                var botInfo = await botClient.GetMeAsync();

                _logger.LogInformation(
                    "Successfully connected to Telegram Bot: {BotUsername} (ID: {BotId})",
                    botInfo.Username, botInfo.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Telegram Bot with provided token");
                return false;
            }
        }

        // ============================================
        // Private Helpers: تنسيق الرسائل
        // ============================================

        /// <summary>
        /// تنسيق رسالة تنبيه انتهاء الاشتراك
        /// </summary>
        private string FormatExpiryWarningMessage(Subscription subscription, int daysRemaining)
        {
            // استخدام HTML formatting
            return $"""
            🔔 <b>تنبيه انتهاء الاشتراك</b>
            
            عزيزي/عزيزتي <b>{subscription.Subscriber.FullName}</b>،
            
            اشتراكك في باقة <b>{subscription.Plan.Name}</b> سينتهي خلال <b>{daysRemaining}</b> {(daysRemaining == 1 ? "يوم" : "أيام")}.
            
            📅 تاريخ الانتهاء: <b>{subscription.EndDate:dd/MM/yyyy}</b>
            💰 المبلغ المطلوب للتجديد: <b>{subscription.Plan.Price:F2}</b> دينار
            
            للتجديد، يرجى التواصل معنا.
            
            شكراً لثقتكم 💚
            """;
        }

        /// <summary>
        /// تنسيق رسالة تأكيد التجديد
        /// </summary>
        private string FormatRenewalConfirmationMessage(Subscription subscription)
        {
            return $"""
            ✅ <b>تم تجديد اشتراكك بنجاح!</b>
            
            عزيزي/عزيزتي <b>{subscription.Subscriber.FullName}</b>،
            
            📦 الباقة: <b>{subscription.Plan.Name}</b>
            ⚡ السرعة: <b>{subscription.Plan.Speed} Mbps</b>
            📅 صالح من: <b>{subscription.StartDate:dd/MM/yyyy}</b>
            📅 صالح حتى: <b>{subscription.EndDate:dd/MM/yyyy}</b>
            
            شكراً لثقتكم 💚
            """;
        }

        /// <summary>
        /// تنسيق رسالة انتهاء الاشتراك
        /// </summary>
        private string FormatExpiredNotificationMessage(Subscription subscription)
        {
            var daysExpired = (DateTime.UtcNow - subscription.EndDate).Days;

            return $"""
            ⚠️ <b>اشتراكك منتهي</b>
            
            عزيزي/عزيزتي <b>{subscription.Subscriber.FullName}</b>،
            
            اشتراكك في باقة <b>{subscription.Plan.Name}</b> منتهي منذ <b>{daysExpired}</b> {(daysExpired == 1 ? "يوم" : "أيام")}.
            
            📅 تاريخ الانتهاء: <b>{subscription.EndDate:dd/MM/yyyy}</b>
            
            للتجديد والاستمرار بالخدمة، يرجى التواصل معنا في أقرب وقت.
            
            شكراً لتفهمكم 💚
            """;
        }
    }
}
