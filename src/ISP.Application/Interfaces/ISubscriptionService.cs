
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscriptions;

namespace ISP.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto dto);
        Task<SubscriptionDto> RenewAsync(RenewSubscriptionDto dto);
        Task<SubscriptionDto?> GetByIdAsync(int id);
        Task<SubscriptionDto?> GetCurrentBySubscriberIdAsync(int subscriberId);
        Task<PagedResultDto<SubscriptionDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task<PagedResultDto<SubscriptionDto>> GetExpiringAsync(int days, int pageNumber = 1, int pageSize = 10);
        Task<PagedResultDto<SubscriptionDto>> GetExpiredAsync(int pageNumber = 1, int pageSize = 10);
        Task UpdateStatusesAsync(); // Background Job
        Task<bool> CancelAsync(int id);
    }
}