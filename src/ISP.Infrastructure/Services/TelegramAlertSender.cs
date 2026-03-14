using System.Text;
using System.Text.Json;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    public class TelegramAlertSender : ITelegramAlertSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramAlertSender> _logger;
        private readonly HttpClient _httpClient;

        public TelegramAlertSender(
            IConfiguration configuration,
            ILogger<TelegramAlertSender> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<bool> SendAlertAsync(SecurityAlert alert)
        {
            // Read sensitive values from User Secrets / appsettings
            var botToken = _configuration["SecurityAlerts:BotToken"];
            var chatId = _configuration["SecurityAlerts:SuperAdminChatId"];
            var enabled = _configuration.GetValue<bool>("SecurityAlerts:EnableAlerts");

            // Skip if alerts are disabled in configuration
            if (!enabled)
            {
                _logger.LogInformation("Security alerts are disabled — skipping Telegram send");
                return false;
            }

            // Skip if credentials are missing
            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
            {
                _logger.LogWarning("Telegram alert failed — BotToken or SuperAdminChatId not configured");
                return false;
            }

            try
            {
                var message = BuildMessage(alert);
                // Build Telegram Bot API endpoint
                var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

                // Build request payload
                var payload = new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                // Serialize payload to JSON and set content type
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                // Send POST request to Telegram API
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    // Alert sent successfully
                    _logger.LogInformation(
                        "Security alert sent via Telegram | AlertId:{AlertId} | Type:{AlertType}",
                        alert.Id, alert.AlertType);

                    return true;
                }

                // Telegram returned an error
                var error = await response.Content.ReadAsStringAsync();

                _logger.LogWarning(
                    "Telegram send failed | AlertId:{AlertId} | Status:{Status} | Error:{Error}",
                    alert.Id, response.StatusCode, error);

                return false;
            }
            catch (Exception ex)
            {
                // Network error or timeout
                _logger.LogError(ex,
                    "Telegram send exception | AlertId:{AlertId}",
                    alert.Id);

                return false;
            }
        }

        // ============================================
        // Helper: Build Telegram Message
        // ============================================
        private string BuildMessage(SecurityAlert alert)
        {
            // Map severity to emoji + label
            var severity = alert.Severity switch
            {
                "Low" => "🟡 Low",
                "Medium" => "🟠 Medium",
                "High" => "🔴 High",
                "Critical" => "🚨 Critical",
                _ => alert.Severity
            };

            // Build formatted HTML message
            var sb = new StringBuilder();

            sb.AppendLine("⚠️ <b>Security Alert</b>");
            sb.AppendLine();
            sb.AppendLine($"<b>Type:</b> {alert.AlertType}");
            sb.AppendLine($"<b>Severity:</b> {severity}");
            sb.AppendLine($"<b>Time:</b> {alert.CreatedAt:yyyy-MM-dd HH:mm} UTC");

            // Include username only if available
            if (!string.IsNullOrEmpty(alert.Username))
                sb.AppendLine($"<b>User:</b> {alert.Username}");

            // Include IP only if available
            if (!string.IsNullOrEmpty(alert.IpAddress))
                sb.AppendLine($"<b>IP:</b> {alert.IpAddress}");

            sb.AppendLine($"<b>Occurrences:</b> {alert.OccurrenceCount}");
            sb.AppendLine();
            sb.AppendLine($"<b>Details:</b> {alert.Message}");
            sb.AppendLine();
            sb.AppendLine("👉 Check Audit Logs for more details.");

            return sb.ToString();
        }
    }
}