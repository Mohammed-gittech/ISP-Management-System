
using ISP.Application.DTOs;
using ISP.Application.DTOs.Tenants;

namespace ISP.Application.Interfaces
{
    public interface ITenantService
    {
        Task<TenantDto> CreateAsync(CreateTenantDto dto);
        Task<TenantDto?> GetByIdAsync(int id);
        Task<PagedResultDto<TenantDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task UpdateAsync(int id, UpdateTenantDto dto);
        Task<bool> DeactivateAsync(int id);
        Task<bool> ActivateAsync(int id);
        Task<int> GetCurrentSubscribersCountAsync(int tenantId);
        Task<bool> CanAddSubscriberAsync(int tenantId);

        // ============================================
        // Subscription Renewal & Payment
        // ============================================

        /// <summary>
        /// TenantAdmin يطلب تجديد اشتراكه
        /// ينشئ TenantSubscription بـ Status = Pending
        /// </summary>
        Task<TenantSubscriptionDto> RenewRequestAsync(int tenantId, RenewTenantSubscriptionDto dto);

        /// <summary>
        /// SuperAdmin يؤكد استلام الدفع ويُفعِّل الـ Tenant
        /// ينشئ TenantPayment ويحدث TenantSubscription
        /// </summary>
        Task ConfirmPaymentAsync(int tenantId, ConfirmTenantPaymentDto dto);

        /// <summary>
        /// SuperAdmin يرى كل الطلبات المعلقة
        /// لمعرفة من يحتاج تأكيد دفع
        /// </summary>
        Task<IEnumerable<TenantSubscriptionDto>> GetPendingRenewalsAsync();
    }
}