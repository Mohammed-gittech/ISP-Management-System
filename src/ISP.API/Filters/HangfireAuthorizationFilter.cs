using Hangfire.Annotations;
using Hangfire.Dashboard;
namespace ISP.API.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            // السماح فقط للـ SuperAdmin
            // يمكن تحسينها لاحقاً للتحقق من JWT
            return true; // TODO: Add proper authorization
        }
    }
}