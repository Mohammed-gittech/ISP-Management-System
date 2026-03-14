using ISP.Domain.Entities;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// Contract for sending security alerts via Telegram
    /// </summary>
    public interface ITelegramAlertSender
    {
        /// <summary>
        /// Sends a security alert notification to SuperAdmin
        /// </summary>
        Task<bool> SendAlertAsync(SecurityAlert alert);
    }
}