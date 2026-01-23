using System.Security.Claims;
using ISP.Application.Interfaces;

namespace ISP.API.Middleware
{
    /// <summary>
    /// Middleware لاستخراج TenantId من JWT Token
    /// وحفظه في CurrentTenantService
    /// </summary>
    public class TenantResolverMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolverMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// يُستدعى لكل HTTP Request
        /// </summary>
        public async Task InvokeAsync(
            HttpContext context,
            ICurrentTenantService currentTenant)
        {
            // 1. التحقق من أن المستخدم مسجل دخول
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // 2. استخراج TenantId من Claims
                var tenantIdClaim = context.User.FindFirst("TenantId");

                if (tenantIdClaim != null && int.TryParse(tenantIdClaim.Value, out int tenantId))
                {
                    // 3. حفظ TenantId في CurrentTenantService
                    currentTenant.SetTenant(tenantId);
                }
                else
                {
                    // SuperAdmin (لا TenantId له)
                    var roleClaim = context.User.FindFirst(ClaimTypes.Role);
                    if (roleClaim?.Value == "SuperAdmin")
                    {
                        currentTenant.SetSuperAdmin();
                    }
                }
            }

            // 4. المتابعة للـ Middleware التالي
            await _next(context);
        }
    }

    /// <summary>
    /// Extension Method لتسهيل التسجيل
    /// </summary>
    public static class TenantResolverMiddlewareExtensions
    {
        public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantResolverMiddleware>();
        }
    }
}