using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage; // ⭐ للـ Transactions

namespace ISP.Infrastructure.Repositories
{
    /// <summary>
    /// Unit of Work - إدارة Transactions
    /// كل التغييرات تُحفظ معاً أو لا شيء
    /// ⭐ محدث: مع Transaction support + InvoiceCounter
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentTenantService _currentTenantService;

        // ⭐ للـ Transaction Management
        private IDbContextTransaction? _currentTransaction;

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
        private IRepository<InvoiceCounter>? _invoiceCounters;

        // Refresh Token
        private IRepository<RefreshToken>? _refreshTokens;

        public UnitOfWork(
            ApplicationDbContext context,
            ICurrentTenantService currentTenantService)
        {
            _context = context;
            _currentTenantService = currentTenantService;
        }

        // ============================================
        // Repository Properties (Lazy Loading)
        // ============================================

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

        // InvoiceCounter Repository
        public IRepository<InvoiceCounter> InvoiceCounters =>
            _invoiceCounters ??= new GenericRepository<InvoiceCounter>(_context, _currentTenantService);

        // Refresh Token
        public IRepository<RefreshToken> RefreshTokens =>
            _refreshTokens ??= new GenericRepository<RefreshToken>(_context, _currentTenantService);

        // ============================================
        // Transaction Methods 
        // ============================================

        /// <summary>
        /// بدء Transaction جديدة
        /// </summary>
        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException("Transaction already in progress");
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Commit التغييرات
        /// </summary>
        public async Task CommitTransactionAsync()
        {
            try
            {
                await SaveChangesAsync();

                if (_currentTransaction != null)
                {
                    await _currentTransaction.CommitAsync();
                }
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }

        /// <summary>
        /// Rollback التغييرات
        /// </summary>
        public async Task RollbackTransactionAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.RollbackAsync();
                }
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
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
            _currentTransaction?.Dispose();
            _context.Dispose();
        }
    }
}