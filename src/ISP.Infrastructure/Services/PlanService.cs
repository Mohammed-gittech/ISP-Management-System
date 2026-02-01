using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Plans;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الباقات
    /// ✅ Soft Delete Support
    /// </summary>
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<PlanService> _logger;

        public PlanService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant,
            ILogger<PlanService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        // ============================================
        // Basic CRUD
        // ============================================

        public async Task<PlanDto> CreateAsync(CreatePlanDto dto)
        {
            var plan = _mapper.Map<Plan>(dto);
            plan.TenantId = _currentTenant.TenantId;
            plan.IsActive = true;

            await _unitOfWork.Plans.AddAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<PlanDto>(plan);
        }

        public async Task<PlanDto?> GetByIdAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);
            return plan == null ? null : _mapper.Map<PlanDto>(plan);
        }

        public async Task<List<PlanDto>> GetActiveAsync()
        {
            var activePlans = await _unitOfWork.Plans.GetAllAsync(p => p.IsActive);
            return _mapper.Map<List<PlanDto>>(activePlans.ToList());
        }

        public async Task<PagedResultDto<PlanDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
        {
            var allPlans = await _unitOfWork.Plans.GetAllAsync();

            var totalCount = allPlans.Count();
            var items = allPlans
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<PlanDto>
            {
                Items = _mapper.Map<List<PlanDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task UpdateAsync(int id, UpdatePlanDto dto)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                throw new InvalidOperationException($"الباقة برقم {id} غير موجودة");

            if (!string.IsNullOrEmpty(dto.Name))
                plan.Name = dto.Name;

            if (dto.Speed.HasValue)
                plan.Speed = dto.Speed.Value;

            if (dto.Price.HasValue)
                plan.Price = dto.Price.Value;

            if (dto.DurationDays.HasValue)
                plan.DurationDays = dto.DurationDays.Value;

            if (dto.Description != null)
                plan.Description = dto.Description;

            if (dto.IsActive.HasValue)
                plan.IsActive = dto.IsActive.Value;

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();
        }

        // ============================================
        // SOFT DELETE (محدث)
        // ============================================

        /// <summary>
        /// حذف ناعم للباقة
        /// ⚠️ لا يمكن حذف باقة مستخدمة في اشتراكات نشطة
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
            {
                _logger.LogWarning("Plan {PlanId} not found for deletion", id);
                return false;
            }

            // التحقق من عدم وجود اشتراكات نشطة
            var activeSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync(s =>
                s.PlanId == id &&
                (s.Status == Domain.Enums.SubscriptionStatus.Active ||
                 s.Status == Domain.Enums.SubscriptionStatus.Expiring));

            if (activeSubscriptions.Any())
            {
                throw new InvalidOperationException(
                    $"لا يمكن حذف الباقة. يوجد {activeSubscriptions.Count()} اشتراك نشط يستخدمها");
            }

            _logger.LogInformation("Soft deleting Plan {PlanId}", id);

            await _unitOfWork.Plans.SoftDeleteAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        // ============================================
        // RESTORE (جديد)
        // ============================================

        public async Task<bool> RestoreAsync(int id)
        {
            _logger.LogInformation("Attempting to restore Plan {PlanId}", id);

            var restored = await _unitOfWork.Plans.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Plan {PlanId} restored successfully", id);
            }

            return restored;
        }

        // ============================================
        // GET DELETED (جديد)
        // ============================================

        public async Task<PagedResultDto<PlanDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10)
        {
            var deleted = await _unitOfWork.Plans.GetDeletedAsync();

            var totalCount = deleted.Count();
            var items = deleted
                .OrderByDescending(p => p.DeletedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<PlanDto>
            {
                Items = _mapper.Map<List<PlanDto>>(items),
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
            _logger.LogWarning("Permanent delete requested for Plan {PlanId}", id);

            var plan = await _unitOfWork.Plans.GetByIdIncludingDeletedAsync(id);

            if (plan == null)
                return false;

            if (!plan.IsDeleted)
            {
                throw new InvalidOperationException("لا يمكن الحذف النهائي لباقة نشطة. استخدم Soft Delete أولاً");
            }

            // التحقق من عدم وجود أي اشتراكات (حتى المحذوفة)
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllIncludingDeletedAsync();
            var planSubs = allSubscriptions.Where(s => s.PlanId == id);

            if (planSubs.Any())
            {
                throw new InvalidOperationException(
                    $"لا يمكن الحذف النهائي. يوجد {planSubs.Count()} اشتراك مرتبط بهذه الباقة");
            }

            await _unitOfWork.Plans.DeleteAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("Plan {PlanId} permanently deleted", id);

            return true;
        }

        // ============================================
        // Activate/Deactivate (موجود مسبقاً)
        // ============================================

        public async Task<bool> DeactivateAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return false;

            plan.IsActive = false;

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(id);

            if (plan == null)
                return false;

            plan.IsActive = true;

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}