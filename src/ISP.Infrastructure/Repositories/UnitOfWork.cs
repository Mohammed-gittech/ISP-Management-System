using ISP.Application.Interfaces;
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
        private readonly ICurrentTenantService _currentTenantService;

        // Lazy initialization
        private IRepository<Tenant>? _tenants;
        private IRepository<TenantSubscription>? _tenantSubscriptions;
        private IRepository<User>? _users;
        private IRepository<Subscriber>? _subscribers;
        private IRepository<Plan>? _plans;
        private IRepository<Subscription>? _subscriptions;
        private IRepository<Notification>? _notifications;
        private IRepository<AuditLog>? _auditLogs;

        // Payment System
        private IRepository<Payment>? _payments;
        private IRepository<Invoice>? _invoices;
        private IRepository<TenantPayment>? _tenantPayments;

        public UnitOfWork(
            ApplicationDbContext context,
            ICurrentTenantService currentTenantService)
        {
            _context = context;
            _currentTenantService = currentTenantService;
        }

        // ============================================
        // Repository Properties
        // ============================================

        /// <summary>
        /// Tenants Repository
        /// Lazy: ينشأ عند أول استخدام فقط
        /// </summary>
        public IRepository<Tenant> Tenants =>
            _tenants ??= new GenericRepository<Tenant>(_context, _currentTenantService);

        public IRepository<TenantSubscription> TenantSubscriptions =>
            _tenantSubscriptions ??= new GenericRepository<TenantSubscription>(_context, _currentTenantService);

        public IRepository<User> Users =>
            _users ??= new GenericRepository<User>(_context, _currentTenantService);

        public IRepository<Subscriber> Subscribers =>
            _subscribers ??= new GenericRepository<Subscriber>(_context, _currentTenantService);

        public IRepository<Plan> Plans =>
            _plans ??= new GenericRepository<Plan>(_context, _currentTenantService);

        public IRepository<Subscription> Subscriptions =>
            _subscriptions ??= new GenericRepository<Subscription>(_context, _currentTenantService);

        public IRepository<Notification> Notifications =>
            _notifications ??= new GenericRepository<Notification>(_context, _currentTenantService);

        public IRepository<AuditLog> AuditLogs =>
            _auditLogs ??= new GenericRepository<AuditLog>(_context, _currentTenantService);

        // Payment System
        public IRepository<Payment> Payments =>
            _payments ??= new GenericRepository<Payment>(_context, _currentTenantService);

        public IRepository<Invoice> Invoices =>
            _invoices ??= new GenericRepository<Invoice>(_context, _currentTenantService);

        public IRepository<TenantPayment> TenantPayments =>
            _tenantPayments ??= new GenericRepository<TenantPayment>(_context, _currentTenantService);

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