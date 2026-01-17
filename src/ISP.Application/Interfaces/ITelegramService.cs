
using ISP.Domain.Entities;

namespace ISP.Application.Interfaces
{
    public interface ITelegramService
    {
        Task<bool> SendMessageAsync(string chatId, string message);
        Task<bool> SendExpiryWarningAsync(Subscription subscription, int daysRemaining);
        Task<bool> SendRenewalConfirmationAsync(Subscription subscription);
        Task<bool> SendExpiredNotificationAsync(Subscription subscription);
        Task<bool> TestConnectionAsync(string botToken);
    }
}