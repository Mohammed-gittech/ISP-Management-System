
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
        Task<int> SaveChangesAsync();
    }
}