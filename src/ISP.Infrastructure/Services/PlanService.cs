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

            // Plan created successfully
            _logger.LogInformation(
                "Plan created successfully | Plan:{PlanId} | Name:{Name} | Tenant:{TenantId} | Speed:{Speed}Mbps | Price:{Price}",
                plan.Id, plan.Name, plan.TenantId, plan.Speed, plan.Price);

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
            {
                _logger.LogWarning(
                    "Plan update failed — not found | Plan:{PlanId}",
                    id);

                throw new InvalidOperationException($"الباقة برقم {id} غير موجودة");
            }

            // Track changes for logging
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(dto.Name))
            {
                changes.Add($"Name:{plan.Name}→{dto.Name}");
                plan.Name = dto.Name;
            }

            if (dto.Speed.HasValue)
            {
                changes.Add($"Speed:{plan.Speed}→{dto.Speed.Value}Mbps");
                plan.Speed = dto.Speed.Value;
            }

            if (dto.Price.HasValue)
            {
                changes.Add($"Price:{plan.Price}→{dto.Price.Value}");
                plan.Price = dto.Price.Value;
            }

            if (dto.DurationDays.HasValue)
            {
                changes.Add($"Duration:{plan.DurationDays}→{dto.DurationDays.Value}days");
                plan.DurationDays = dto.DurationDays.Value;
            }

            if (dto.Description != null)
            {
                changes.Add("Description:updated");
                plan.Description = dto.Description;
            }

            if (dto.IsActive.HasValue)
            {
                changes.Add($"IsActive:{plan.IsActive}→{dto.IsActive.Value}");
                plan.IsActive = dto.IsActive.Value;
            }

            await _unitOfWork.Plans.UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            // Plan updated successfully
            _logger.LogInformation(
                "Plan updated successfully | Plan:{PlanId} | Tenant:{TenantId} | Changes:{Changes}",
                id, plan.TenantId, string.Join(", ", changes));
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
                _logger.LogWarning(
                    "Plan deletion failed — not found | Plan:{PlanId}",
                    id);

                return false;
            }

            // Check active subscriptions
            var activeSubscriptions = await _unitOfWork.Subscriptions.GetAllAsync(s =>
                s.PlanId == id &&
                (s.Status == Domain.Enums.SubscriptionStatus.Active ||
                s.Status == Domain.Enums.SubscriptionStatus.Expiring));

            if (activeSubscriptions.Any())
            {
                _logger.LogWarning(
                    "Plan deletion blocked — has active subscriptions | Plan:{PlanId} | Count:{Count}",
                    id, activeSubscriptions.Count());

                throw new InvalidOperationException(
                    $"لا يمكن حذف الباقة. يوجد {activeSubscriptions.Count()} اشتراك نشط يستخدمها");
            }

            await _unitOfWork.Plans.SoftDeleteAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            // Plan soft deleted
            _logger.LogWarning(
                "Plan soft deleted | Plan:{PlanId} | Name:{Name} | Tenant:{TenantId}",
                id, plan.Name, plan.TenantId);

            return true;
        }

        // ============================================
        // RESTORE (جديد)
        // ============================================

        public async Task<bool> RestoreAsync(int id)
        {
            var plan = await _unitOfWork.Plans.GetByIdIncludingDeletedAsync(id);

            if (plan == null || !plan.IsDeleted)
            {
                _logger.LogWarning(
                    "Plan restore failed — not found or not deleted | Plan:{PlanId}",
                    id);

                return false;
            }

            var restored = await _unitOfWork.Plans.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Plan restored successfully | Plan:{PlanId} | Name:{Name} | Tenant:{TenantId}",
                    id, plan.Name, plan.TenantId);
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
            var plan = await _unitOfWork.Plans.GetByIdIncludingDeletedAsync(id);

            if (plan == null)
                return false;

            if (plan == null)
            {
                _logger.LogWarning(
                    "Plan permanent delete failed — not found | Plan:{PlanId}",
                    id);

                return false;
            }

            if (!plan.IsDeleted)
            {
                _logger.LogWarning(
                    "Plan permanent delete blocked — not soft deleted | Plan:{PlanId}",
                    id);

                throw new InvalidOperationException("لا يمكن الحذف النهائي لباقة نشطة. استخدم Soft Delete أولاً");
            }


            // Check all subscriptions including deleted
            var allSubscriptions = await _unitOfWork.Subscriptions.GetAllIncludingDeletedAsync();
            var planSubs = allSubscriptions.Where(s => s.PlanId == id).ToList();

            if (planSubs.Any())
            {
                _logger.LogWarning(
                    "Plan permanent delete blocked — has linked subscriptions | Plan:{PlanId} | Count:{Count}",
                    id, planSubs.Count);

                throw new InvalidOperationException(
                    $"لا يمكن الحذف النهائي. يوجد {planSubs.Count} اشتراك مرتبط بهذه الباقة");
            }

            await _unitOfWork.Plans.DeleteAsync(plan);
            await _unitOfWork.SaveChangesAsync();

            // Critical — cannot be undone
            _logger.LogCritical(
                "Plan PERMANENTLY DELETED | Plan:{PlanId} | Name:{Name} | Tenant:{TenantId}",
                id, plan.Name, plan.TenantId);

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