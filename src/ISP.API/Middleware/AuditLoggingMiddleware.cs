// ============================================
// AuditLoggingMiddleware.cs - تسجيل تلقائي للعمليات
// ============================================
using System.Text;
using ISP.Application.Interfaces;

namespace ISP.API.Middleware
{
    public class AuditLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuditLoggingMiddleware> _logger;

        public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService)
        {
            // تجاهل الـ Endpoints التي لا نريد تسجيلها
            var path = context.Request.Path.ToString().ToLower();
            if (ShouldSkipLogging(path))
            {
                await _next(context);
                return;
            }

            // حفظ الـ Response Body الأصلي
            var originalBodyStream = context.Response.Body;

            try
            {
                // استخراج معلومات الـ Request
                var method = context.Request.Method;
                var endpoint = context.Request.Path;
                var queryString = context.Request.QueryString.ToString();

                // قراءة Body (للـ POST/PUT)
                // string requestBody = await ReadRequestBodyAsync(context.Request);

                string requestBody = await ReadAndSanitizeRequestBodyAsync(context.Request);

                // استبدال Response Body بـ MemoryStream لنتمكن من قراءته
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                // تنفيذ الـ Request
                var startTime = DateTime.UtcNow;
                await _next(context);
                var duration = DateTime.UtcNow - startTime;

                // قراءة الـ Response
                var statusCode = context.Response.StatusCode;
                responseBody.Seek(0, SeekOrigin.Begin);
                var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);

                // إرجاع الـ Response للـ Client
                await responseBody.CopyToAsync(originalBodyStream);

                // تحديد نوع العملية
                var action = DetermineAction(method, endpoint, statusCode);
                var entityType = DetermineEntityType(endpoint);
                var success = statusCode >= 200 && statusCode < 300;

                // تسجيل في Database
                if (!string.IsNullOrEmpty(action))
                {
                    await auditLogService.LogAsync(
                        action: action,
                        entityType: entityType,
                        entityId: null, // يمكن استخراجه من الـ Response JSON
                        oldValues: null,
                        newValues: method == "POST" || method == "PUT" ? requestBody : null,
                        success: success,
                        errorMessage: success ? null : $"Status Code: {statusCode}"
                    );
                }

                _logger.LogInformation(
                    "Request: {Method} {Path} | Status: {StatusCode} | Duration: {Duration}ms",
                    method, endpoint, statusCode, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                // تسجيل الخطأ
                await auditLogService.LogAsync(
                    action: "Error",
                    entityType: "Request",
                    success: false,
                    errorMessage: ex.Message
                );

                _logger.LogError(ex, "Error in AuditLoggingMiddleware");
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        // ============================================
        // Helper: قراءة Request Body
        // ============================================
        private async Task<string> ReadAndSanitizeRequestBodyAsync(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0)
                return string.Empty;

            request.EnableBuffering();

            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            // إزالة البيانات الحساسة
            return SanitizeSensitiveData(body);
        }

        // ============================================
        // تنظيف البيانات الحساسة من JSON
        // ============================================
        private string SanitizeSensitiveData(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;

            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var sanitized = new Dictionary<string, object>();

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var key = property.Name.ToLower();

                    // قائمة الحقول الحساسة
                    if (key == "password" ||
                        key == "oldpassword" ||
                        key == "newpassword" ||
                        key == "confirmpassword" ||
                        key == "passwordhash" ||
                        key == "token" ||
                        key == "apikey" ||
                        key == "secret")
                    {
                        sanitized[property.Name] = "***REDACTED***";
                    }
                    else
                    {
                        sanitized[property.Name] = property.Value.ToString();
                    }
                }

                return System.Text.Json.JsonSerializer.Serialize(sanitized);
            }
            catch
            {
                // إذا فشل الـ parsing، نرجع النص كما هو
                return json;
            }
        }

        // private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        // {
        //     if (request.ContentLength == null || request.ContentLength == 0)
        //         return string.Empty;

        //     request.EnableBuffering(); // السماح بقراءة الـ Body أكثر من مرة

        //     using var reader = new StreamReader(
        //         request.Body,
        //         encoding: Encoding.UTF8,
        //         detectEncodingFromByteOrderMarks: false,
        //         bufferSize: 1024,
        //         leaveOpen: true);

        //     var body = await reader.ReadToEndAsync();
        //     request.Body.Position = 0; // إعادة الـ Stream للبداية

        //     return body;
        // }

        // ============================================
        // Helper: تحديد نوع العملية
        // ============================================
        private string DetermineAction(string method, string path, int statusCode)
        {
            // Login/Logout
            if (path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
                return statusCode == 200 ? "Login" : "LoginFailed";

            if (path.Contains("/auth/logout", StringComparison.OrdinalIgnoreCase))
                return "Logout";

            // CRUD Operations
            return method switch
            {
                "POST" => "Create",
                "PUT" => "Update",
                "PATCH" => "Update",
                "DELETE" => "Delete",
                "GET" => "View",
                _ => "Unknown"
            };
        }

        // ============================================
        // Helper: تحديد نوع الكيان
        // ============================================
        private string DetermineEntityType(string path)
        {
            path = path.ToLower();

            if (path.Contains("/users")) return "User";
            if (path.Contains("/tenants")) return "Tenant";
            if (path.Contains("/subscribers")) return "Subscriber";
            if (path.Contains("/plans")) return "Plan";
            if (path.Contains("/subscriptions")) return "Subscription";
            if (path.Contains("/notifications")) return "Notification";
            if (path.Contains("/auditlogs")) return "AuditLog";

            return "Unknown";
        }

        // ============================================
        // Helper: تجاهل Endpoints معينة
        // ============================================
        private bool ShouldSkipLogging(string path)
        {
            var skipPaths = new[]
            {
                "/swagger",
                "/health",
                "/favicon.ico",
                "/hangfire",
                "/_framework",
                "/css",
                "/js"
            };

            return skipPaths.Any(skip => path.Contains(skip, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class AuditLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuditLoggingMiddleware>();
        }
    }
}