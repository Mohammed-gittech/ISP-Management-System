using Microsoft.AspNetCore.Authorization;

namespace ISP.Application.Authorization
{
    /// <summary>
    /// Requirement that enforces tenant ownership check
    /// The actual logic is in TenantOwnershipHandler
    /// </summary>
    public class TenantOwnershipRequirement : IAuthorizationRequirement
    {
        // Intentionally empty — requirement acts as a marker only
        // All logic is handled in TenantOwnershipHandler
    }
}