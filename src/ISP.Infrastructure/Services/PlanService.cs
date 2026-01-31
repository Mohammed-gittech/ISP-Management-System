using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Plans;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الباقات
    /// </summary>
    public class PlanService : IPlanService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;

        public PlanService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentTenantService currentTenant)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
        }

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