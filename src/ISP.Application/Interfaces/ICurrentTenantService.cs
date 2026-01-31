
namespace ISP.Application.Interfaces
{
    public interface ICurrentTenantService
    {
        int TenantId { get; }
        bool IsSuperAdmin { get; }
        int? UserId { get; }
        string? Username { get; }
        void SetTenant(int tenantId);
        void SetSuperAdmin();

        // Todo Remov if it dont work
        bool HasTenant { get; }
    }
}