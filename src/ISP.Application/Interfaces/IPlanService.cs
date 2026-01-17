
using ISP.Application.DTOs;
using ISP.Application.DTOs.Plans;

namespace ISP.Application.Interfaces
{
    public interface IPlanService
    {
        Task<PlanDto> CreateAsync(CreatePlanDto dto);
        Task<PlanDto?> GetByIdAsync(int id);
        Task<List<PlanDto>> GetActiveAsync();
        Task<PagedResultDto<PlanDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10);
        Task UpdateAsync(int id, UpdatePlanDto dto);
        Task<bool> DeactivateAsync(int id);
        Task<bool> ActivateAsync(int id);

    }
}