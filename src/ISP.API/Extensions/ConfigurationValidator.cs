using Microsoft.Extensions.Configuration;

namespace ISP.API.Extensions
{
    /// <summary>
    /// Configuration Validator - يتحقق من وجود كل Secrets المطلوبة
    /// </summary>
    public static class ConfigurationValidator
    {
        /// <summary>
        /// التحقق من كل الإعدادات المطلوبة عند بدء التطبيق
        /// </summary>
        public static void ValidateRequiredSettings(this IConfiguration configuration)
        {
            var errors = new List<string>();

            // ============================================
            // 1. Database Connection Strings
            // ============================================
            var defaultConnection = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConnection))
            {
                errors.Add("❌ ConnectionStrings:DefaultConnection is missing or empty");
            }

            var hangfireConnection = configuration.GetConnectionString("HangfireConnection");
            if (string.IsNullOrWhiteSpace(hangfireConnection))
            {
                errors.Add("❌ ConnectionStrings:HangfireConnection is missing or empty");
            }

            // ============================================
            // 2. JWT Configuration
            // ============================================
            var jwtKey = configuration["JWT:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                errors.Add("❌ JWT:Key is missing or empty");
            }
            else if (jwtKey.Length < 32)
            {
                errors.Add($"❌ JWT:Key must be at least 32 characters long (current: {jwtKey.Length})");
            }

            var jwtIssuer = configuration["JWT:Issuer"];
            if (string.IsNullOrWhiteSpace(jwtIssuer))
            {
                errors.Add("❌ JWT:Issuer is missing or empty");
            }

            var jwtAudience = configuration["JWT:Audience"];
            if (string.IsNullOrWhiteSpace(jwtAudience))
            {
                errors.Add("❌ JWT:Audience is missing or empty");
            }

            // ============================================
            // 3. إذا كانت هناك أخطاء، أوقف التطبيق
            // ============================================
            if (errors.Any())
            {
                var errorMessage = string.Join(Environment.NewLine, errors);

                throw new InvalidOperationException(
                    $"{Environment.NewLine}" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Environment.NewLine}" +
                    $"🔒 CONFIGURATION ERROR: Missing Required Settings{Environment.NewLine}" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"{errorMessage}{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Environment.NewLine}" +
                    $"💡 SOLUTION:{Environment.NewLine}" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"📌 Development Environment:{Environment.NewLine}" +
                    $"   1. Navigate to: cd src/ISP.API{Environment.NewLine}" +
                    $"   2. Initialize: dotnet user-secrets init{Environment.NewLine}" +
                    $"   3. Set secrets: dotnet user-secrets set \"<KEY>\" \"<VALUE>\"{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"   Example:{Environment.NewLine}" +
                    $"   dotnet user-secrets set \"JWT:Key\" \"YourSecretKey123...\"{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"📌 Production Environment:{Environment.NewLine}" +
                    $"   Set environment variables:{Environment.NewLine}" +
                    $"   export JWT__Key=\"YourSecretKey123...\"{Environment.NewLine}" +
                    $"   export ConnectionStrings__DefaultConnection=\"Server=...\"{Environment.NewLine}" +
                    $"{Environment.NewLine}" +
                    $"📖 See SECRETS_SETUP.md for detailed instructions{Environment.NewLine}" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{Environment.NewLine}"
                );
            }
        }

        /// <summary>
        /// طباعة ملخص Configuration (بدون كشف Secrets)
        /// </summary>
        public static void LogConfigurationSummary(this IConfiguration configuration, ILogger logger)
        {
            logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            logger.LogInformation("🔒 Configuration Summary (Secrets Hidden)");
            logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // Database
            var hasDb = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection"));
            logger.LogInformation("Database Connection: {Status}", hasDb ? "✅ Configured" : "❌ Missing");

            // JWT
            var jwtKey = configuration["JWT:Key"];
            var hasJwt = !string.IsNullOrWhiteSpace(jwtKey);
            var jwtLength = jwtKey?.Length ?? 0;
            logger.LogInformation("JWT Key: {Status} ({Length} chars)",
                hasJwt ? "✅ Configured" : "❌ Missing",
                jwtLength);

            // Hangfire
            var hasHangfire = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("HangfireConnection"));
            logger.LogInformation("Hangfire Connection: {Status}", hasHangfire ? "✅ Configured" : "❌ Missing");

            logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
    }
}