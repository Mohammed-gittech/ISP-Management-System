using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الاشتراكات
    /// </summary>
    /// 
    public class SubscriptionService : ISubscriptionService
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;

        public SubscriptionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
        }

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
            subscription.Plan = plan; // لحساب EndDate
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

            // 4. تحديث الاشتراك القديم
            oldSubscription.Status = SubscriptionStatus.Expired;
            await _unitOfWork.Subscriptions.UpdateAsync(oldSubscription);

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
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

            var current = allSubscriptions
                .Where(s => s.SubscriberId == subscriberId)
                .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Expiring)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

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
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

            var expiringDate = DateTime.UtcNow.AddDays(days);

            var expiring = allSubscriptions
                .Where(s => s.EndDate.Date <= expiringDate.Date && s.EndDate.Date >= DateTime.UtcNow.Date)
                .OrderBy(s => s.EndDate)
                .ToList();

            var totalCount = expiring.Count;
            var items = expiring
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
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync();

            var expired = allSubscriptions
                .Where(s => s.Status == SubscriptionStatus.Expired)
                .OrderByDescending(s => s.EndDate)
                .ToList();

            var totalCount = expired.Count;
            var items = expired
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

        public async Task<bool> CancelAsync(int id)
        {
            var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(id);

            if (subscription == null)
                return false;

            subscription.Status = SubscriptionStatus.Expired;

            await _unitOfWork.Subscriptions.UpdateAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}