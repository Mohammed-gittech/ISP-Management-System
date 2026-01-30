using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة Multi-Tenancy - تحدد TenantId الحالي
    /// </summary>
    public class CurrentTenantService : ICurrentTenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private int? _tenantId;
        private bool? _isSuperAdmin;

        public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// TenantId الحالي
        /// </summary>
        public int TenantId
        {
            get
            {
                if (_tenantId == null)
                    throw new InvalidOperationException("Tenant context not set.");
                return _tenantId.Value;
            }
        }

        /// <summary>
        /// هل المستخدم SuperAdmin؟
        /// </summary>
        public bool IsSuperAdmin => _isSuperAdmin ?? false;

        /// <summary>
        /// UserId الحالي (من JWT)
        /// </summary>
        public int? UserId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return string.IsNullOrEmpty(claim) ? null : int.Parse(claim);
            }
        }

        /// <summary>
        /// Username الحالي (من JWT)
        /// </summary>
        public string? Username
        {
            get
            {
                return _httpContextAccessor.HttpContext?.User
                    .FindFirst(ClaimTypes.Name)?.Value;
            }
        }

        public void SetTenant(int tenantId)
        {
            if (_tenantId != null)
                throw new InvalidOperationException("Tenant already set.");
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