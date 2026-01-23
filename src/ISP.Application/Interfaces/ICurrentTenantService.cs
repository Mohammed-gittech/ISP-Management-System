
namespace ISP.Application.Interfaces
{
    public interface ICurrentTenantService
    {
        int TenantId { get; }
        bool IsSuperAdmin { get; }
        void SetTenant(int tenantId);
        void SetSuperAdmin();
    }
}