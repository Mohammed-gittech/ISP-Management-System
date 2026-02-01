using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscribers;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// Subscriber Service Interface
    /// ✅ Soft Delete Support
    /// </summary>
    public interface ISubscriberService
    {
        // ============================================
        // Basic CRUD
        // ============================================
        Task<SubscriberDto> CreateAsync(CreateSubscriberDto dto);
        Task<SubscriberDto?> GetByIdAsync(int id);
        Task<PagedResultDto<SubscriberDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task<PagedResultDto<SubscriberDto>> SearchAsync(string searchTerm, int pageNumber = 1, int pageSize = 10);
        Task UpdateAsync(int id, UpdateSubscriberDto dto);

        /// <summary>
        /// حذف ناعم - يحذف Subscriptions المرتبطة
        /// </summary>
        Task DeleteAsync(int id);

        // ============================================
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// استرجاع مشترك محذوف
        /// </summary>
        Task<bool> RestoreAsync(int id);

        /// <summary>
        /// الحصول على المشتركين المحذوفين
        /// </summary>
        Task<PagedResultDto<SubscriberDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10);

        /// <summary>
        /// حذف نهائي (SuperAdmin only)
        /// </summary>
        Task<bool> PermanentDeleteAsync(int id);

        // ============================================
        // Helper Methods
        // ============================================
        Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeId = null);
        Task<bool> LinkTelegramAsync(int subscriberId, string chatId);
    }
}