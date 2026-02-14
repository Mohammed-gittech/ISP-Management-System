using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ISP.Infrastructure.Data
{
    /// <summary>
    /// DbContext الرئيسي للتطبيق
    /// ✅ Multi-Tenancy Support
    /// ✅ Soft Delete Global Query Filter
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
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

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Subscriber> Subscribers => Set<Subscriber>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        // Payment System
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<TenantPayment> TenantPayments => Set<TenantPayment>();
        public DbSet<InvoiceCounter> InvoiceCounters => Set<InvoiceCounter>(); // ⭐ NEW

        // ============================================
        // OnModelCreating - تكوين Model
        // ============================================

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================
            // تطبيق Configurations تلقائياً
            // ============================================
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // ============================================
            // GLOBAL QUERY FILTER - SOFT DELETE
            // ============================================
            ApplySoftDeleteQueryFilter(modelBuilder);
        }

        /// <summary>
        /// تطبيق Global Query Filter للـ Soft Delete
        /// يُطبق تلقائياً على كل Queries
        /// Filter: WHERE IsDeleted = 0
        /// </summary>
        private void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
        {
            // الحصول على كل Entity Types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                // التحقق: هل Entity يرث من BaseEntity؟
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    // بناء Expression: entity => !entity.IsDeleted
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, "IsDeleted");
                    var notDeleted = Expression.Not(property); // !IsDeleted = IsDeleted == false
                    var lambda = Expression.Lambda(notDeleted, parameter);

                    // تطبيق Filter على Entity
                    entityType.SetQueryFilter(lambda);
                }
            }
        }
    }
}