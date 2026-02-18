using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.SqlServer;
using ISP.API.Extensions;
using ISP.API.Middleware;
using ISP.Application.Interfaces;
using ISP.Application.Mappings;
using ISP.Application.Validators;
using ISP.Domain.Interfaces;
using ISP.Infrastructure;
using ISP.Infrastructure.BackgroundJobs;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Identity;
using ISP.Infrastructure.Repositories;
using ISP.Infrastructure.Services;
using ISP.Infrastructure.Services.Notifications;
using ISP.Infrastructure.Services.Telegram;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ✅ Validate Configuration عند البداية
builder.Configuration.ValidateRequiredSettings();

// Add services to the container.
// ============================================
// Database
// ============================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("ISP.Infrastructure") //مهم!
    )
);

// ============================================
// Hangfire Configuration
// ============================================
builder.Services.AddHangfire(config =>
{
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            builder.Configuration.GetConnectionString("HangfireConnection"),
            new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            });
});

// Add Hangfire Server
builder.Services.AddHangfireServer();

// ============================================
// Authentication & JWT
// ============================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// ============================================
// AutoMapper
// ============================================
builder.Services.AddAutoMapper(typeof(AutoMapperProfile).Assembly);

// ============================================
// FluentValidation
// ============================================

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateTenantValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSubscriberValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePlanValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSubscriptionValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateSubscriberValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCashPaymentValidator>();

// ============================================
// Repository & Unit of Work
// ============================================
builder.Services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ============================================
// Identity Services
// ============================================
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// ============================================
// Business Services
// ============================================
builder.Services.AddScoped<ICurrentTenantService, CurrentTenantService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISubscriberService, SubscriberService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();

// ============================================
// Phase 2: Telegram & Notification Services
// ============================================
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ============================================
// Phase 2: Background Jobs
// ============================================
builder.Services.AddScoped<NotificationJob>();
builder.Services.AddScoped<SubscriptionStatusJob>();
builder.Services.AddScoped<RetentionCleanupJob>();

// ============================================
// Phase 3: Users Management Service
// ============================================
builder.Services.AddScoped<IUserService, UserService>();

// ============================================
// Phase 3: Validators
// ============================================
builder.Services.AddValidatorsFromAssemblyContaining<CreateUserValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateUserValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<ChangePasswordValidator>();

// ============================================
// Phase 3: Audit Log Service
// ============================================
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ============================================
// Payment System Services
// ============================================
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// ============================================
// Reports & Analytics Service
// ============================================
builder.Services.AddScoped<IReportService, ReportService>();

// ============================================
// HttpContextAccessor (مطلوب للـ IP Address)
// ============================================
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
// Register Swagger generator and customize its behavior.
builder.Services.AddSwaggerGen(options =>
{
    // ===============================
    // 1) Define the JWT Bearer security scheme
    // ===============================
    //
    // This tells Swagger that our API uses JWT Bearer authentication
    // through the HTTP Authorization header.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        // The name of the HTTP header where the token will be sent.
        Name = "Authorization",


        // Indicates this is an HTTP authentication scheme.
        Type = SecuritySchemeType.Http,


        // Specifies the authentication scheme name.
        // Must be exactly "Bearer" for JWT Bearer tokens.
        Scheme = "Bearer",


        // Optional metadata to describe the token format.
        BearerFormat = "JWT",


        // Specifies that the token is sent in the request header.
        In = ParameterLocation.Header,


        // Text shown in Swagger UI to guide the user.
        Description = "Enter: Bearer {your JWT token}"
    });


    // ===============================
    // 2) Require the Bearer scheme for secured endpoints
    // ===============================
    //
    // This tells Swagger that endpoints protected by [Authorize]
    // require the Bearer token defined above.
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                // Reference the previously defined "Bearer" security scheme.
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },


            // No scopes are required for JWT Bearer authentication.
            // This array is empty because JWT does not use OAuth scopes here.
            new string[] {}
        }
    });
});

var app = builder.Build();

// ✅ Log Configuration Summary
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    builder.Configuration.LogConfigurationSummary(logger);
}

// ============================================
// Middleware Pipeline
// ============================================

// 1. Exception Handling
app.UseExceptionHandling();

// 2. Swagger (Development)
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. HTTPS Redirection
app.UseHttpsRedirection();

// 4. Authentication
app.UseAuthentication(); // ← قبل Authorization

// 5. Tenant Resolver (بعد Authentication)
app.UseTenantResolver();

// ============================================
// Phase 3: Audit Logging Middleware
// ============================================
app.UseAuditLogging();

app.UseAuthorization();

// ============================================
// Hangfire Dashboard
// ============================================
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // ⚠️ للتطوير فقط - في Production استخدم Authorization
    Authorization = new[] { new HangfireAuthorizationFilter() }
});
// ============================================
// Schedule Background Jobs
// ============================================
ConfigureBackgroundJobs(app.Services);

app.MapControllers();

app.Run();

// ============================================
// Background Jobs Configuration
// ============================================
void ConfigureBackgroundJobs(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // الحصول على Cron Schedules من appsettings.json
    var expiryCheckCron = config["BackgroundJobs:ExpiryCheckCron"] ?? "0 1 * * *";
    var retryFailedCron = config["BackgroundJobs:RetryFailedCron"] ?? "0 */6 * * *";
    var statusUpdateCron = config["BackgroundJobs:StatusUpdateCron"] ?? "0 2 * * *";

    // Job 1: فحص وإرسال تنبيهات انتهاء الاشتراك (Daily at 1 AM)
    RecurringJob.AddOrUpdate<NotificationJob>(
        "send-expiry-notifications",
        job => job.SendExpiryNotificationsAsync(),
        expiryCheckCron,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    // Job 2: إعادة محاولة الإشعارات الفاشلة (Every 6 hours)
    RecurringJob.AddOrUpdate<NotificationJob>(
        "retry-failed-notifications",
        job => job.RetryFailedNotificationsAsync(),
        retryFailedCron,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    // Job 3: تحديث حالات الاشتراكات (Daily at 2 AM)
    RecurringJob.AddOrUpdate<SubscriptionStatusJob>(
        "update-subscription-statuses",
        job => job.UpdateSubscriptionStatusesAsync(),
        statusUpdateCron,
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    // Job 4: إرسال تنبيهات الاشتراكات المنتهية (Daily at 3 AM)
    RecurringJob.AddOrUpdate<NotificationJob>(
        "send-expired-notifications",
        job => job.SendExpiredNotificationsAsync(),
        "0 3 * * *",
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    // Job 5 (Optional): تنظيف الإشعارات القديمة (Weekly on Sunday at 4 AM)
    RecurringJob.AddOrUpdate<SubscriptionStatusJob>(
        "cleanup-old-notifications",
        job => job.CleanupOldNotificationsAsync(),
        "0 4 * * 0", // Every Sunday at 4 AM
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    // Job 6 (Optional): إحصائيات يومية (Daily at 5 AM)
    RecurringJob.AddOrUpdate<SubscriptionStatusJob>(
        "generate-daily-statistics",
        job => job.GenerateDailyStatisticsAsync(),
        "0 5 * * *",
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );

    //جدولة Retention Cleanup Job
    RecurringJob.AddOrUpdate<RetentionCleanupJob>(
        "retention-cleanup",
        job => job.ExecuteAsync(),
        Cron.Daily(2, 0), // يعمل كل يوم الساعة 2:00 صباحاً
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Local
        });
}

// ============================================
// Hangfire Authorization Filter (للتطوير)
// ============================================
public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // ⚠️ للتطوير فقط - يسمح للجميع
        // في Production: تحقق من Authentication
        return true;
    }
}