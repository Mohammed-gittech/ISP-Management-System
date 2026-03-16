using System.Security.Claims;
using ISP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ISP.Application.Authorization
{
    /// <summary>
    /// Handles tenant ownership verification for protected resources
    /// SuperAdmin has access to everything
    /// TenantAdmin can only access resources belonging to their tenant
    /// </summary>
    public class TenantOwnershipHandler
        : AuthorizationHandler<TenantOwnershipRequirement, ITenantOwnedResource>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TenantOwnershipRequirement requirement,
            ITenantOwnedResource resource)
        {
            // Read current user's role from JWT
            var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

            // Case 1 — SuperAdmin has access to everything
            if (userRole == "SuperAdmin")
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Case 2 — Resource belongs to no tenant (SuperAdmin-only resource)
            if (resource.TenantId == null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // Case 3 — TenantAdmin: verify tenant ownership
            var tenantIdClaim = context.User.FindFirst("TenantId")?.Value;

            if (string.IsNullOrEmpty(tenantIdClaim))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // Safe parse — avoids exception on invalid claim value
            if (!int.TryParse(tenantIdClaim, out var currentTenantId))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (currentTenantId == resource.TenantId)
                context.Succeed(requirement);
            else
                context.Fail();

            return Task.CompletedTask;
        }
    }
}