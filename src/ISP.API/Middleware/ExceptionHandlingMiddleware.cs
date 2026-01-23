using System.Net;
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
            // 1. Logging
            _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

            // 2. تحديد Status Code
            var statusCode = exception switch
            {
                InvalidOperationException => HttpStatusCode.BadRequest,        // 400
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,    // 401
                KeyNotFoundException => HttpStatusCode.NotFound,               // 404
                _ => HttpStatusCode.InternalServerError                        // 500
            };

            // 3. بناء Response
            var response = new
            {
                Success = false,
                Message = exception.Message,
                StatusCode = (int)statusCode,
                // StackTrace فقط في Development
                StackTrace = _env.IsDevelopment() ? exception.StackTrace : null
            };

            // 4. إرجاع JSON Response
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    /// <summary>
    /// Extension Method
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}