
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ISP.Infrastructure.Data
{
    /// <summary>
    /// DbContext الرئيسي للتطبيق
    /// يمثل الجلسة (Session) مع Database
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Constructor - يستقبل Options من DI Container
        /// Options تحتوي على Connection String
        /// </summary>

        private readonly ICurrentTenantService? _currentTenant;
        public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenant)
         : base(options)
        {
            _currentTenant = currentTenant;
        }

        // ============================================
        // DbSets - تمثل الجداول في Database
        // ============================================

        /// <summary>
        /// جدول Tenants (الوكلاء)
        /// Set<T>() هو Property ديناميكي من DbContext
        /// </summary>

        public DbSet<Tenant> Tenants => Set<Tenant>();

        /// <summary>
        /// جدول TenantSubscriptions (اشتراكات الوكلاء)
        /// </summary>
        public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

        /// <summary>
        /// جدول Users (المستخدمين)
        /// </summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>
        /// جدول Subscribers (المشتركين)
        /// </summary>
        public DbSet<Subscriber> Subscribers => Set<Subscriber>();

        /// <summary>
        /// جدول Plans (الباقات)
        /// </summary>
        public DbSet<Plan> Plans => Set<Plan>();

        /// <summary>
        /// جدول Subscriptions (الاشتراكات)
        /// </summary>
        public DbSet<Subscription> Subscriptions => Set<Subscription>();

        /// <summary>
        /// جدول Notifications (الإشعارات)
        /// </summary>
        public DbSet<Notification> Notifications => Set<Notification>();

        /// <summary>
        /// جدول AuditLogs (سجلات العمليات)
        /// </summary>
        public DbSet<AuditLog> AuditLogs { get; set; }

        // ============================================
        // OnModelCreating - يُستدعى عند بناء Model
        // هنا نطبق Fluent API Configurations
        // ============================================

        /// <summary>
        /// تكوين Model (Schema) للـ Database
        /// </summary>
        /// <param name="modelBuilder">Builder لتكوين Entities</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // استدعاء Base method
            base.OnModelCreating(modelBuilder);

            // ============================================
            // تطبيق كل Configurations تلقائياً
            // يبحث عن كل class يرث من IEntityTypeConfiguration
            // في نفس Assembly
            // ============================================
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

            // Query Filters لا تُضاف هنا!
            // سنضيفها بطريقة مختلفة لاحقاً (Phase 2)
            // Todo: إضافة Query Filters للـ Multi-Tenancy
        }
    }
}