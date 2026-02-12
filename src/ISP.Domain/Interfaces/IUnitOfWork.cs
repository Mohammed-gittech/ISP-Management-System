using ISP.Domain.Entities;

namespace ISP.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
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

        Task<int> SaveChangesAsync();
    }
}