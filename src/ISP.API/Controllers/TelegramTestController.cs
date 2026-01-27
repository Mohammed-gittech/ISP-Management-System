using ISP.Application.Interfaces;
using ISP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller للاختبار اليدوي لـ Telegram Integration
    /// ⚠️ للتطوير فقط - احذفه في Production
    /// </summary>
    [ApiController]
    [Route("api/test/telegram")]
    [AllowAnonymous] // ⚠️ للاختبار فقط - بدون Authentication
    public class TelegramTestController : ControllerBase
    {
        private readonly ITelegramService _telegramService;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TelegramTestController> _logger;

        public TelegramTestController(
            ITelegramService telegramService,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            ILogger<TelegramTestController> logger)
        {
            _telegramService = telegramService;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ============================================
        // Test 1: اختبار Token
        // ============================================

        /// <summary>
        /// اختبار الاتصال بـ Telegram Bot
        /// </summary>
        /// <param name="botToken">Token الخاص بالبوت</param>
        /// <returns>معلومات البوت إذا نجح الاتصال</returns>
        [HttpPost("test-token")]
        public async Task<IActionResult> TestBotToken([FromBody] TestTokenRequest request)
        {
            try
            {
                _logger.LogInformation("Testing Telegram Bot Token...");

                bool isValid = await _telegramService.TestConnectionAsync(request.BotToken);

                if (isValid)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "✅ Token صحيح! البوت يعمل بنجاح",
                        BotToken = MaskToken(request.BotToken)
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "❌ Token خاطئ أو البوت غير موجود"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing bot token");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"خطأ في الاختبار: {ex.Message}"
                });
            }
        }

        // ============================================
        // Test 2: إرسال رسالة بسيطة
        // ============================================

        /// <summary>
        /// إرسال رسالة تجريبية لـ ChatId معين
        /// </summary>
        [HttpPost("send-test-message")]
        public async Task<IActionResult> SendTestMessage([FromBody] SendTestMessageRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Sending test message to ChatId: {ChatId}, Tenant: {TenantId}",
                    request.ChatId, request.TenantId);

                bool success = await _telegramService.SendMessageAsync(
                    request.TenantId,
                    request.ChatId,
                    request.Message ?? "🎉 مرحباً! هذه رسالة تجريبية من نظام ISP Management System"
                );

                if (success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "✅ تم إرسال الرسالة بنجاح!",
                        TenantId = request.TenantId,
                        ChatId = request.ChatId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "❌ فشل إرسال الرسالة (تحقق من Logs)"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test message");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"خطأ: {ex.Message}"
                });
            }
        }



        // ============================================
        // Test 3: إرسال Expiry Warning
        // ============================================

        /// <summary>
        /// إرسال رسالة تنبيه انتهاء اشتراك (تجريبية)
        /// </summary>
        [HttpPost("send-expiry-warning/{subscriptionId}")]
        public async Task<IActionResult> SendExpiryWarning(int subscriptionId, [FromQuery] int daysRemaining = 3)
        {
            try
            {
                // 1. الحصول على Subscription مع Relations
                var subscription = await GetSubscriptionWithRelationsAsync(subscriptionId);

                if (subscription == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"Subscription {subscriptionId} not found"
                    });
                }

                // 2. إرسال التنبيه
                bool success = await _telegramService.SendExpiryWarningAsync(
                    subscription.TenantId,
                    subscription,
                    daysRemaining
                );

                if (success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = $"✅ تم إرسال تنبيه الانتهاء لـ {subscription.Subscriber.FullName}",
                        SubscriptionId = subscriptionId,
                        DaysRemaining = daysRemaining
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "❌ فشل إرسال التنبيه",
                        Reason = "Subscriber has no Telegram ChatId or Token invalid"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending expiry warning");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"خطأ: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// إرسال رسالة تأكيد التجديد (تجريبية)
        /// </summary>
        [HttpPost("send-renewal-confirmation/{subscriptionId}")]
        public async Task<IActionResult> SendRenewalConfirmation(int subscriptionId)
        {
            try
            {
                var subscription = await GetSubscriptionWithRelationsAsync(subscriptionId);

                if (subscription == null)
                {
                    return NotFound(new
                    {
                        Success = false,
                        Message = $"Subscription {subscriptionId} not found"
                    });
                }

                bool success = await _telegramService.SendRenewalConfirmationAsync(
                    subscription.TenantId,
                    subscription
                );

                if (success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = $"✅ تم إرسال تأكيد التجديد لـ {subscription.Subscriber.FullName}",
                        SubscriptionId = subscriptionId
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "❌ فشل إرسال التأكيد"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending renewal confirmation");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"خطأ: {ex.Message}"
                });
            }
        }

        // ============================================
        // Test 5: اختبار NotificationService
        // ============================================

        /// <summary>
        /// إنشاء وإرسال إشعار كامل (NotificationService)
        /// </summary>
        [HttpPost("test-notification-service")]
        public async Task<IActionResult> TestNotificationService([FromBody] TestNotificationRequest request)
        {
            try
            {
                _logger.LogInformation(
                    "Testing NotificationService: Subscriber={SubscriberId}, Tenant={TenantId}",
                    request.SubscriberId, request.TenantId);

                // Create and Send في خطوة واحدة
                bool success = await _notificationService.CreateAndSendAsync(
                    request.TenantId,
                    request.SubscriberId,
                    Domain.Enums.NotificationType.SystemAlert,
                    request.Message ?? "🧪 رسالة تجريبية من NotificationService",
                    Domain.Enums.NotificationChannel.Telegram
                );

                if (success)
                {
                    return Ok(new
                    {
                        Success = true,
                        Message = "✅ تم إنشاء وإرسال الإشعار بنجاح!",
                        Note = "تحقق من جدول Notifications في Database"
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "❌ فشل إنشاء أو إرسال الإشعار"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing notification service");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"خطأ: {ex.Message}"
                });
            }
        }

        // ============================================
        // Helper Methods
        // ============================================

        /// <summary>
        /// الحصول على Subscription مع كل Relations
        /// </summary>
        private async Task<Domain.Entities.Subscription?> GetSubscriptionWithRelationsAsync(int subscriptionId)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(subscriptionId);

            if (subscription == null)
                return null;

            // Load Relations manually
            subscription.Subscriber = (await _unitOfWork.Subscribers.GetByIdAsync(subscription.SubscriberId))!;
            subscription.Plan = (await _unitOfWork.Plans.GetByIdAsync(subscription.PlanId))!;
            subscription.Tenant = (await _unitOfWork.Tenants.GetByIdAsync(subscription.TenantId))!;

            return subscription;
        }

        /// <summary>
        /// إخفاء Token (للأمان)
        /// </summary>
        private string MaskToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "Not Set";

            if (token.Length <= 10)
                return "***";

            return $"{token[..5]}...{token[^5..]}";
        }
    }

    // ============================================
    // Request DTOs
    // ============================================

    public record TestTokenRequest(string BotToken);

    public record SendTestMessageRequest(
        int TenantId,
        string ChatId,
        string? Message = null
    );

    public record TestNotificationRequest(
        int TenantId,
        int SubscriberId,
        string? Message = null
    );
}