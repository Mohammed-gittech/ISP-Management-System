
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscribers;

namespace ISP.Application.Interfaces
{
    public interface ISubscriberService
    {
        Task<SubscriberDto> CreateAsync(CreateSubscriberDto dto);
        Task<SubscriberDto?> GetByIdAsync(int id);
        Task<PagedResultDto<SubscriberDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task<PagedResultDto<SubscriberDto>> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);
        Task UpdateAsync(int id, UpdateSubscriberDto dto);
        Task DeleteAsync(int id);
        Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeId = null);
        Task<bool> LinkTelegramAsync(int subscriberId, string chatId);
    }
}