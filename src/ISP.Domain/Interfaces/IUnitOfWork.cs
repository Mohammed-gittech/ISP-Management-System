using ISP.Domain.Entities;
namespace ISP.Domain.Interfaces
{
    /// <summary>
    /// Unit of Work Pattern
    /// يوفر وصول موحد لجميع Repositories
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // ============================================
        // Repositories
        // ============================================
        IRepository<Tenant> Tenants { get; }
        IRepository<TenantSubscription> TenantSubscriptions { get; }
        IRepository<User> Users { get; }
        IRepository<Subscriber> Subscribers { get; }
        IRepository<Plan> Plans { get; }
        IRepository<Subscription> Subscriptions { get; }
        IRepository<Notification> Notifications { get; }
        IRepository<AuditLog> AuditLogs { get; }

        // Payment System
        IRepository<Payment> Payments { get; }
        IRepository<Invoice> Invoices { get; }
        IRepository<TenantPayment> TenantPayments { get; }
        IRepository<InvoiceCounter> InvoiceCounters { get; }

        // RefreshTokens
        IRepository<RefreshToken> RefreshTokens { get; }

        // ============================================
        // Transaction Methods 
        // ============================================

        /// <summary>
        /// بدء Transaction جديدة
        /// </summary>
        Task BeginTransactionAsync();

        /// <summary>
        /// Commit التغييرات
        /// </summary>
        Task CommitTransactionAsync();

        /// <summary>
        /// Rollback التغييرات
        /// </summary>
        Task RollbackTransactionAsync();

        // ============================================
        // Save Changes
        // ============================================

        /// <summary>
        /// حفظ جميع التغييرات
        /// </summary>
        Task<int> SaveChangesAsync();
    }
}