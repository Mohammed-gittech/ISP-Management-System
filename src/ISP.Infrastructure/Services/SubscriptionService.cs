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
            // Check subscriber exists
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(dto.SubscriberId);
            if (subscriber == null)
                throw new InvalidOperationException("المشترك غير موجود");

            // Check plan exists and active
            var plan = await _unitOfWork.Plans.GetByIdAsync(dto.PlanId);
            if (plan == null || !plan.IsActive)
            {
                _logger.LogWarning(
                    "Subscription creation failed — plan not found or inactive | Plan:{PlanId}",
                    dto.PlanId);

                throw new InvalidOperationException("الباقة غير موجودة أو غير نشطة");
            }

            // Create subscription
            var subscription = _mapper.Map<Subscription>(dto);
            subscription.TenantId = _currentTenant.TenantId;
            subscription.Plan = plan;
            subscription.CreatedAt = DateTime.UtcNow;

            subscription.CalculateEndDate();

            subscription.UpdateStatus();

            await _unitOfWork.Subscriptions.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            // Subscription created successfully
            _logger.LogInformation(
                "Subscription created | Subscription:{SubscriptionId} | Subscriber:{SubscriberId} | Plan:{PlanId} | Tenant:{TenantId} | EndDate:{EndDate}",
                subscription.Id, dto.SubscriberId, dto.PlanId,
                subscription.TenantId, subscription.EndDate.ToString("yyyy-MM-dd"));

            return _mapper.Map<SubscriptionDto>(subscription);
        }

        public async Task<SubscriptionDto> RenewAsync(RenewSubscriptionDto dto)
        {
            // Check old subscription exists
            var oldSubscription = await _unitOfWork.Subscriptions.GetByIdAsync(dto.SubscriptionId);
            if (oldSubscription == null)
            {
                _logger.LogWarning(
                    "Subscription renewal failed — not found | Subscription:{SubscriptionId}",
                    dto.SubscriptionId);

                throw new InvalidOperationException("الاشتراك غير موجود");
            }

            // Determine plan
            var planId = dto.NewPlanId ?? oldSubscription.PlanId;
            var plan = await _unitOfWork.Plans.GetByIdAsync(planId);

            if (plan == null || !plan.IsActive)
            {
                _logger.LogWarning(
                    "Subscription renewal failed — plan not found or inactive | Plan:{PlanId}",
                    planId);

                throw new InvalidOperationException("الباقة غير صالحة");
            }

            // Create new subscription
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

            // Soft delete old subscription
            await _unitOfWork.Subscriptions.SoftDeleteAsync(oldSubscription);

            // Save new subscription
            await _unitOfWork.Subscriptions.AddAsync(newSubscription);
            await _unitOfWork.SaveChangesAsync();

            // Subscription renewed successfully
            _logger.LogInformation(
                "Subscription renewed | Old:{OldSubscriptionId} → New:{NewSubscriptionId} | Subscriber:{SubscriberId} | Plan:{PlanId} | Tenant:{TenantId} | EndDate:{EndDate}",
                dto.SubscriptionId, newSubscription.Id, newSubscription.SubscriberId,
                planId, newSubscription.TenantId, newSubscription.EndDate.ToString("yyyy-MM-dd"));

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
        // SOFT DELETE 
        // ============================================

        /// <summary>
        /// إلغاء اشتراك (Soft Delete)
        /// </summary>
        public async Task<bool> CancelAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(id);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Subscription cancellation failed — not found | Subscription:{SubscriptionId}",
                    id);

                return false;
            }

            // Soft Delete
            await _unitOfWork.Subscriptions.SoftDeleteAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            // Subscription cancelled
            _logger.LogWarning(
                "Subscription cancelled | Subscription:{SubscriptionId} | Subscriber:{SubscriberId} | Tenant:{TenantId} | Plan:{PlanId}",
                id, subscription.SubscriberId, subscription.TenantId, subscription.PlanId);

            return true;
        }

        // ============================================
        // RESTORE 
        // ============================================

        public async Task<bool> RestoreAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdIncludingDeletedAsync(id);

            if (subscription == null || !subscription.IsDeleted)
            {
                _logger.LogWarning(
                    "Subscription restore failed — not found or not deleted | Subscription:{SubscriptionId}",
                    id);

                return false;
            }

            var restored = await _unitOfWork.Subscriptions.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Subscription restored | Subscription:{SubscriptionId} | Subscriber:{SubscriberId} | Tenant:{TenantId}",
                    id, subscription.SubscriberId, subscription.TenantId);
            }

            return restored;
        }

        // ============================================
        // GET DELETED 
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
        // PERMANENT DELETE 
        // ============================================

        public async Task<bool> PermanentDeleteAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdIncludingDeletedAsync(id);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Subscription permanent delete failed — not found | Subscription:{SubscriptionId}",
                    id);

                return false;
            }

            if (!subscription.IsDeleted)
            {
                _logger.LogWarning(
                    "Subscription permanent delete blocked — not cancelled | Subscription:{SubscriptionId}",
                    id);

                throw new InvalidOperationException("لا يمكن الحذف النهائي لاشتراك نشط. استخدم Cancel أولاً");
            }

            await _unitOfWork.Subscriptions.DeleteAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            // Critical — cannot be undone
            _logger.LogCritical(
                "Subscription PERMANENTLY DELETED | Subscription:{SubscriptionId} | Subscriber:{SubscriberId} | Tenant:{TenantId} | Plan:{PlanId}",
                id, subscription.SubscriberId, subscription.TenantId, subscription.PlanId);

            return true;
        }
    }
}