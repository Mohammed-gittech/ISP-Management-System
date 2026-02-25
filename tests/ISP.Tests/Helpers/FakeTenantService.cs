// ============================================
// FakeTenantService.cs
// Fake implementation of ICurrentTenantService
// Used only in Integration Tests
// ============================================
using ISP.Application.Interfaces;

namespace ISP.Tests.Helpers
{
    public class FakeTenantService : ICurrentTenantService
    {
        private int? _tenantId;
        private bool _isSuperAdmin;
        private int? _userId;
        private string? _username;

        // Constructor للـ Tenant عادي
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

        // ✅ Constructor جديد — يقبل TenantId و UserId و Username معاً
        // يُستخدم في AuditLog Tests التي تحتاج Username في السجل
        public FakeTenantService(int tenantId, int userId, string username)
        {
            _tenantId = tenantId;
            _isSuperAdmin = false;
            _userId = userId;
            _username = username;
        }

        // ============================================
        // ICurrentTenantService Implementation
        // ============================================

        public int TenantId => _tenantId
            ?? throw new InvalidOperationException("Tenant context not set.");

        public bool IsSuperAdmin => _isSuperAdmin;

        public bool HasTenant => _tenantId != null;

        public int? UserId => _userId;

        public string? Username => _username;

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