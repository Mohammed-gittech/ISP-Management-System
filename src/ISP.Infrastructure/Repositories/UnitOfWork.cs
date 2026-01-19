
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Data;

namespace ISP.Infrastructure.Repositories
{
    /// <summary>
    /// Unit of Work - إدارة Transactions
    /// كل التغييرات تُحفظ معاً أو لا شيء
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly ApplicationDbContext _context;

        // Lazy initialization للـ Repositories
        private IRepository<Tenant>? _tenants;
        private IRepository<TenantSubscription>? _tenantSubscriptions;
        private IRepository<User>? _users;
        private IRepository<Subscriber>? _subscribers;
        private IRepository<Plan>? _plans;
        private IRepository<Subscription>? _subscriptions;
        private IRepository<Notification>? _notifications;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================
        // Repository Properties
        // ============================================

        /// <summary>
        /// Tenants Repository
        /// Lazy: ينشأ عند أول استخدام فقط
        /// </summary>
        public IRepository<Tenant> Tenants
        {
            get
            {
                _tenants ??= new GenericRepository<Tenant>(_context);
                return _tenants;
            }
        }

        public IRepository<TenantSubscription> TenantSubscriptions
        {
            get
            {
                _tenantSubscriptions ??= new GenericRepository<TenantSubscription>(_context);
                return _tenantSubscriptions;
            }
        }

        public IRepository<User> Users
        {
            get
            {
                _users ??= new GenericRepository<User>(_context);
                return _users;
            }
        }

        public IRepository<Subscriber> Subscribers
        {
            get
            {
                _subscribers ??= new GenericRepository<Subscriber>(_context);
                return _subscribers;
            }
        }

        public IRepository<Plan> Plans
        {
            get
            {
                _plans ??= new GenericRepository<Plan>(_context);
                return _plans;
            }
        }

        public IRepository<Subscription> Subscriptions
        {
            get
            {
                _subscriptions ??= new GenericRepository<Subscription>(_context);
                return _subscriptions;
            }
        }

        public IRepository<Notification> Notifications
        {
            get
            {
                _notifications ??= new GenericRepository<Notification>(_context);
                return _notifications;
            }
        }

        // ============================================
        // SaveChanges - Transaction
        // ============================================

        /// <summary>
        /// حفظ كل التغييرات في Transaction واحد
        /// لو واحد فشل، كلهم Rollback
        /// </summary>
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        // ============================================
        // Dispose
        // ============================================

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}