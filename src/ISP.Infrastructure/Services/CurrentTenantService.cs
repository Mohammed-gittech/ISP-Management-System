using ISP.Application.Interfaces;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة Multi-Tenancy - تحدد TenantId الحالي
    /// </summary>
    public class CurrentTenantService : ICurrentTenantService
    {
        private int? _tenantId;
        private bool? _isSuperAdmin;

        /// <summary>
        /// TenantId الحالي
        /// يُستخرج من JWT Token في Middleware
        /// </summary>
        public int TenantId
        {
            get
            {
                if (_tenantId == null)
                    throw new InvalidOperationException("Tenant context not set. User must be authenticated.");

                return _tenantId.Value;
            }
        }

        /// <summary>
        /// هل المستخدم الحالي SuperAdmin؟
        /// SuperAdmin يرى كل الوكلاء
        /// </summary>
        public bool IsSuperAdmin
        {
            get => _isSuperAdmin ?? false;
        }

        /// <summary>
        /// تعيين TenantId
        /// يُستدعى من Middleware بعد قراءة JWT
        /// </summary>
        public void SetTenant(int tenantId)
        {
            if (_tenantId != null)
                throw new InvalidOperationException("Tenant already set for this request.");

            _tenantId = tenantId;
            _isSuperAdmin = false;
        }

        /// <summary>
        /// تعيين SuperAdmin mode
        /// </summary>
        public void SetSuperAdmin()
        {
            _isSuperAdmin = true;
            _tenantId = null; // SuperAdmin لا TenantId له
        }
    }
}