using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الاشتراكات
    /// ✅ Soft Delete Support
    /// </summary>
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant,
            ILogger<SubscriptionService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        // ============================================
        // Basic CRUD
        // ============================================

        public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionDto dto)
        {
            // 1. التحقق من المشترك
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(dto.SubscriberId);
            if (subscriber == null)
                throw new InvalidOperationException("المشترك غير موجود");

            // 2. التحقق من الباقة
            var plan = await _unitOfWork.Plans.GetByIdAsync(dto.PlanId);
            if (plan == null || !plan.IsActive)
                throw new InvalidOperationException("الباقة غير موجودة أو غير نشطة");

            // 3. إنشاء الاشتراك
            var subscription = _mapper.Map<Subscription>(dto);
            subscription.TenantId = _currentTenant.TenantId;
            subscription.Plan = plan;
            subscription.CreatedAt = DateTime.UtcNow;

            // 4. حساب تاريخ الانتهاء
            subscription.CalculateEndDate();

            // 5. تحديد الحالة
            subscription.UpdateStatus();

            // 6. حفظ
            await _unitOfWork.Subscriptions.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<SubscriptionDto>(subscription);
        }

        public async Task<SubscriptionDto> RenewAsync(RenewSubscriptionDto dto)
        {
            // 1. الحصول على الاشتراك القديم
            var oldSubscription = await _unitOfWork.Subscriptions.GetByIdAsync(dto.SubscriptionId);
            if (oldSubscription == null)
                throw new InvalidOperationException("الاشتراك غير موجود");

            // 2. تحديد الباقة (نفسها أو جديدة)
            var planId = dto.NewPlanId ?? oldSubscription.PlanId;
            var plan = await _unitOfWork.Plans.GetByIdAsync(planId);

            if (plan == null || !plan.IsActive)
                throw new InvalidOperationException("الباقة غير صالحة");

            // 3. إنشاء اشتراك جديد
            var newSubscription = new Subscription
            {
                TenantId = _currentTenant.TenantId,
                SubscriberId = oldSubscription.SubscriberId,
                PlanId = planId,
                Plan = plan,
                StartDate = DateTime.UtcNow,
                AutoRenew = dto.AutoRenew,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            newSubscription.CalculateEndDate();
            newSubscription.UpdateStatus();

            // 4. تحديث الاشتراك القديم (Soft Delete)
            await _unitOfWork.Subscriptions.SoftDeleteAsync(oldSubscription);

            // 5. حفظ الجديد
            await _unitOfWork.Subscriptions.AddAsync(newSubscription);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<SubscriptionDto>(newSubscription);
        }

        public async Task<SubscriptionDto?> GetByIdAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(id);
            return subscription == null ? null : _mapper.Map<SubscriptionDto>(subscription);
        }

        public async Task<SubscriptionDto?> GetCurrentBySubscriberIdAsync(int subscriberId)
        {
            var subscriptions = await _unitOfWork.Subscriptions.GetAllAsync(s =>
                s.SubscriberId == subscriberId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expiring));

            var current = subscriptions.OrderByDescending(s => s.CreatedAt).FirstOrDefault();

            return current == null ? null : _mapper.Map<SubscriptionDto>(current);
        }

        public async Task<PagedResultDto<SubscriptionDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
        {
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

            var totalCount = allSubscriptions.Count();
            var items = allSubscriptions
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriptionDto>
            {
                Items = _mapper.Map<List<SubscriptionDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PagedResultDto<SubscriptionDto>> GetExpiringAsync(int days, int pageNumber = 1, int pageSize = 10)
        {
            var expiringDate = DateTime.UtcNow.AddDays(days);

            var expiring = await _unitOfWork.Subscriptions.GetAllAsync(s =>
                s.EndDate.Date <= expiringDate.Date && s.EndDate.Date >= DateTime.UtcNow.Date);

            var sorted = expiring.OrderBy(s => s.EndDate).ToList();

            var totalCount = sorted.Count;
            var items = sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriptionDto>
            {
                Items = _mapper.Map<List<SubscriptionDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PagedResultDto<SubscriptionDto>> GetExpiredAsync(int pageNumber = 1, int pageSize = 10)
        {
            var expired = await _unitOfWork.Subscriptions.GetAllAsync(s => s.Status == SubscriptionStatus.Expired);

            var sorted = expired.OrderByDescending(s => s.EndDate).ToList();

            var totalCount = sorted.Count;
            var items = sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriptionDto>
            {
                Items = _mapper.Map<List<SubscriptionDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task UpdateStatusesAsync()
        {
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

            foreach (var subscription in allSubscriptions)
            {
                var oldStatus = subscription.Status;
                subscription.UpdateStatus();

                if (oldStatus != subscription.Status)
                {
                    await _unitOfWork.Subscriptions.UpdateAsync(subscription);
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        // ============================================
        // SOFT DELETE (محدث)
        // ============================================

        /// <summary>
        /// إلغاء اشتراك (Soft Delete)
        /// </summary>
        public async Task<bool> CancelAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(id);

            if (subscription == null)
                return false;

            _logger.LogInformation("Canceling (soft deleting) Subscription {SubscriptionId}", id);

            // Soft Delete
            await _unitOfWork.Subscriptions.SoftDeleteAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        // ============================================
        // RESTORE (جديد)
        // ============================================

        public async Task<bool> RestoreAsync(int id)
        {
            _logger.LogInformation("Attempting to restore Subscription {SubscriptionId}", id);

            var restored = await _unitOfWork.Subscriptions.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Subscription {SubscriptionId} restored successfully", id);
            }

            return restored;
        }

        // ============================================
        // GET DELETED (جديد)
        // ============================================

        public async Task<PagedResultDto<SubscriptionDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10)
        {
            var deleted = await _unitOfWork.Subscriptions.GetDeletedAsync();

            var totalCount = deleted.Count();
            var items = deleted
                .OrderByDescending(s => s.DeletedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriptionDto>
            {
                Items = _mapper.Map<List<SubscriptionDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // PERMANENT DELETE (جديد)
        // ============================================

        public async Task<bool> PermanentDeleteAsync(int id)
        {
            _logger.LogWarning("Permanent delete requested for Subscription {SubscriptionId}", id);

            var subscription = await _unitOfWork.Subscriptions.GetByIdIncludingDeletedAsync(id);

            if (subscription == null)
                return false;

            if (!subscription.IsDeleted)
            {
                throw new InvalidOperationException("لا يمكن الحذف النهائي لاشتراك نشط. استخدم Cancel أولاً");
            }

            await _unitOfWork.Subscriptions.DeleteAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("Subscription {SubscriptionId} permanently deleted", id);

            return true;
        }
    }
}