// ============================================
// FakeTenantService.cs
// Fake implementation of ICurrentTenantService
// Used only in Integration Tests
// ============================================
// Why: The real CurrentTenantService depends on
// IHttpContextAccessor which doesn't exist in tests.
// This Fake gives us full control over TenantId.
// ============================================
using ISP.Application.Interfaces;

namespace ISP.Tests.Helpers
{
    public class FakeTenantService : ICurrentTenantService
    {
        // نخزن TenantId و IsSuperAdmin مباشرة بدون HttpContext
        private int? _tenantId;
        private bool _isSuperAdmin;

        // Constructor يقبل TenantId مباشرة
        public FakeTenantService(int tenantId)
        {
            _tenantId = tenantId;
            _isSuperAdmin = false;
        }

        // Constructor للـ SuperAdmin
        public FakeTenantService(bool isSuperAdmin = false)
        {
            _isSuperAdmin = isSuperAdmin;
            _tenantId = null;
        }

        // ============================================
        // ICurrentTenantService Implementation
        // ============================================

        public int TenantId => _tenantId
            ?? throw new InvalidOperationException("Tenant context not set.");

        public bool IsSuperAdmin => _isSuperAdmin;

        public bool HasTenant => _tenantId != null;

        // لا نحتاجهما في الاختبارات
        public int? UserId => null;
        public string? Username => null;

        public void SetTenant(int tenantId)
        {
            _tenantId = tenantId;
            _isSuperAdmin = false;
        }

        public void SetSuperAdmin()
        {
            _isSuperAdmin = true;
            _tenantId = null;
        }
    }
}