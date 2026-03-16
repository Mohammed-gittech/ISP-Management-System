using System.Security.Claims;
using ISP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Base controller providing shared helper methods for all controllers
    /// Not registered as a controller — inheriting controllers use its helpers
    /// </summary>
    public abstract class BaseController : ControllerBase
    {
        protected readonly IAuthorizationService _authorizationService;

        protected BaseController(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        // ============================================
        // Role Helpers
        // ============================================

        /// <summary>
        /// Returns true if current user is SuperAdmin
        /// </summary>
        protected bool IsSuperAdmin()
            => User.FindFirst(ClaimTypes.Role)?.Value == "SuperAdmin";

        /// <summary>
        /// Returns true if current user is TenantAdmin
        /// </summary>
        protected bool IsTenantAdmin()
            => User.FindFirst(ClaimTypes.Role)?.Value == "TenantAdmin";

        // ============================================
        // Claim Helpers
        // ============================================

        /// <summary>
        /// Returns current user's TenantId from JWT
        /// Returns 0 if not found or invalid
        /// </summary>
        protected int GetCurrentTenantId()
        {
            int.TryParse(User.FindFirst("TenantId")?.Value, out var tenantId);
            return tenantId;
        }

        /// <summary>
        /// Returns current user's UserId from JWT
        /// Returns 0 if not found or invalid
        /// </summary>
        protected int GetCurrentUserId()
        {
            int.TryParse(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out var userId);
            return userId;
        }

        // ============================================
        // Ownership Helpers
        // ============================================

        /// <summary>
        /// Checks ownership of a tenant-owned resource
        /// Returns Forbid() if access is denied
        /// Returns null if access is granted
        /// </summary>
        protected async Task<IActionResult?> CheckOwnershipAsync(
            ITenantOwnedResource resource)
        {
            var result = await _authorizationService
                .AuthorizeAsync(User, resource, "TenantOwnership");

            return result.Succeeded ? null : Forbid();
        }

        /// <summary>
        /// Returns true if TenantAdmin is trying to access another tenant
        /// SuperAdmin always returns false (has access to everything)
        /// </summary>
        protected bool IsCrossTenantAccess(int targetTenantId)
        {
            if (IsSuperAdmin()) return false;
            return GetCurrentTenantId() != targetTenantId;
        }
    }
}