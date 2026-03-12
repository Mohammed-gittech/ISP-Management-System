using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace ISP.API.Middleware
{
    /// <summary>
    /// Middleware معالجة الأخطاء بشكل مركزي
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // محاولة تنفيذ الـ Request
                await _next(context);
            }
            catch (Exception ex)
            {
                // في حالة حدوث Exception
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Collect request information for logging
            var method = context.Request.Method;
            var path = context.Request.Path;
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var tenantId = context.User.FindFirst("TenantId")?.Value ?? "-";

            // Log based on exception type
            switch (exception)
            {
                case InvalidOperationException:
                case KeyNotFoundException:
                case UnauthorizedAccessException:
                    // Expected errors — user sent bad data or unauthorized access
                    _logger.LogWarning(
                        "Expected exception | {Method} {Path} | User:{UserId} Tenant:{TenantId} | {Message}",
                        method, path, userId, tenantId, exception.Message);
                    break;

                default:
                    // Unexpected errors — real system failure
                    _logger.LogError(
                        exception,
                        "Unhandled exception | {Method} {Path} | User:{UserId} Tenant:{TenantId} | {Message}",
                        method, path, userId, tenantId, exception.Message);
                    break;
            }

            // Determine status code
            var statusCode = exception switch
            {
                InvalidOperationException => HttpStatusCode.BadRequest,        // 400
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,    // 401
                KeyNotFoundException => HttpStatusCode.NotFound,               // 404
                _ => HttpStatusCode.InternalServerError                        // 500
            };

            // Build response
            var response = new
            {
                Success = false,
                Message = exception.Message,
                StatusCode = (int)statusCode,
                // Show stack trace in development only
                StackTrace = _env.IsDevelopment() ? exception.StackTrace : null
            };

            // Return JSON response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        }
    }

    /// <summary>
    /// Extension Method
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
            => builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}