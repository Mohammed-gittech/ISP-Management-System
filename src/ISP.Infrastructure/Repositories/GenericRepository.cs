
using System.Linq.Expressions;
using ISP.Application.Interfaces;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISP.Infrastructure.Repositories
{
    /// <summary>
    /// Generic Repository - CRUD عام لكل Entities
    /// </summary>
    /// <typeparam name="T">Entity Type</typeparam>
    public class GenericRepository<T> : IRepository<T> where T : class
    {
        /// <summary>
        /// DbContext للوصول للـ Database
        /// </summary>
        protected readonly ApplicationDbContext _context;

        /// <summary>
        /// DbSet<T> للـ Entity المحدد
        /// مثلاً: DbSet<Subscriber>
        /// </summary>
        protected readonly DbSet<T> _dbSet;

        /// <summary>
        /// Current Tenant Service - للحصول على TenantId الحالي
        /// </summary>
        private readonly ICurrentTenantService _currentTenantService;


        /// <summary>
        /// Constructor - يستقبل DbContext من DI
        /// </summary>
        public GenericRepository(ApplicationDbContext context, ICurrentTenantService currentTenantService)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _currentTenantService = currentTenantService;
        }

        // ============================================
        // Get Methods
        // ============================================

        // ============================================
        // GET BY ID - مع Multi-Tenancy
        // ============================================
        public async Task<T?> GetByIdAsync(int id)
        {
            var query = _dbSet.Where(e => EF.Property<int>(e, "Id") == id);
            query = ApplyTenantFilter(query);
            return await query.FirstOrDefaultAsync();
        }

        // ============================================
        // GET ALL - مع Multi-Tenancy
        // ============================================
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query);
            return await query.ToListAsync();
        }

        // ============================================
        // GET ALL with Predicate
        // ============================================
        public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query);
            return await query.ToListAsync();
        }

        // ============================================
        // GET BY TENANT
        // ============================================
        public async Task<IEnumerable<T>> GetByTenantAsync(int tenantId)
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query, tenantId);
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> GetByTenantAsync(int tenantId, Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query, tenantId);
            return await query.ToListAsync();
        }

        // ============================================
        // COUNT
        // ============================================
        public async Task<int> CountAsync()
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query);
            return await query.CountAsync();
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query);
            return await query.CountAsync();
        }

        // ============================================
        // EXISTS
        // ============================================
        public async Task<bool> ExistsAsync(int id)
        {
            var query = _dbSet.Where(e => EF.Property<int>(e, "Id") == id);
            query = ApplyTenantFilter(query);
            return await query.AnyAsync();
        }

        // ============================================
        // Create, Update, Delete
        // ============================================

        // ============================================
        // ADD - مع تعيين TenantId تلقائياً
        // ============================================
        public async Task<T> AddAsync(T entity)
        {
            SetTenantId(entity);
            await _dbSet.AddAsync(entity);
            return entity;
        }

        // ============================================
        // UPDATE
        // ============================================
        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        // ============================================
        // DELETE
        // ============================================
        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        // ============================================
        // HELPER: Apply Tenant Filter (Expression Tree)
        // ============================================
        // Todo removet letter
        // private IQueryable<T> ApplyTenantFilter(IQueryable<T> query, int? specificTenantId = null)
        // {
        //     // 1. التحقق: هل Entity يدعم Multi-Tenancy؟
        //     var tenantIdProperty = typeof(T).GetProperty("TenantId");
        //     if (tenantIdProperty == null)
        //         return query; // Entity بدون TenantId (مثل Tenant نفسه)

        //     // 2. SuperAdmin يرى كل شيء (إلا إذا حددنا TenantId معين)
        //     if (_currentTenantService.IsSuperAdmin && specificTenantId == null)
        //         return query;

        //     // 3. تحديد TenantId المطلوب
        //     var targetTenantId = specificTenantId ?? _currentTenantService.TenantId;

        //     // 4. بناء Expression: entity => entity.TenantId == targetTenantId
        //     var parameter = Expression.Parameter(typeof(T), "entity");
        //     var property = Expression.Property(parameter, tenantIdProperty);

        //     Expression comparison;
        //     if (tenantIdProperty.PropertyType == typeof(int?))
        //     {
        //         // Nullable: entity.TenantId.HasValue && entity.TenantId.Value == targetTenantId
        //         var hasValue = Expression.Property(property, "HasValue");
        //         var value = Expression.Property(property, "Value");
        //         var constant = Expression.Constant(targetTenantId, typeof(int));
        //         comparison = Expression.AndAlso(hasValue, Expression.Equal(value, constant));
        //     }
        //     else
        //     {
        //         // Non-nullable: entity.TenantId == targetTenantId
        //         var constant = Expression.Constant(targetTenantId, typeof(int));
        //         comparison = Expression.Equal(property, constant);
        //     }

        //     var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
        //     return query.Where(lambda);
        // }

        private IQueryable<T> ApplyTenantFilter(IQueryable<T> query, int? specificTenantId = null)
        {
            // 1. التحقق: هل Entity يدعم Multi-Tenancy؟
            var tenantIdProperty = typeof(T).GetProperty("TenantId");
            if (tenantIdProperty == null)
                return query; // Entity بدون TenantId (مثل Tenant نفسه)

            // 2. SuperAdmin يرى كل شيء (إلا إذا حددنا TenantId معين)
            if (_currentTenantService.IsSuperAdmin && specificTenantId == null)
                return query;

            // 3. تحديد TenantId المطلوب (nullable)
            int? targetTenantId = specificTenantId;

            if (!targetTenantId.HasValue)
            {
                // جرب تأخذ TenantId من CurrentTenantService إذا معرف
                // فرضًا أن TenantId في _currentTenantService هو int? أو يوفر خاصية HasTenant
                if (_currentTenantService.HasTenant)
                    targetTenantId = _currentTenantService.TenantId;
            }

            if (!targetTenantId.HasValue)
            {
                // لا يوجد TenantId، نعيد الاستعلام بدون فلترة (مفيد للـ Login مثلا)
                return query;
            }

            // 4. بناء Expression: entity => entity.TenantId == targetTenantId
            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Property(parameter, tenantIdProperty);

            Expression comparison;
            if (tenantIdProperty.PropertyType == typeof(int?))
            {
                var hasValue = Expression.Property(property, "HasValue");
                var value = Expression.Property(property, "Value");
                var constant = Expression.Constant(targetTenantId.Value, typeof(int));
                comparison = Expression.AndAlso(hasValue, Expression.Equal(value, constant));
            }
            else
            {
                var constant = Expression.Constant(targetTenantId.Value, typeof(int));
                comparison = Expression.Equal(property, constant);
            }

            var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
            return query.Where(lambda);
        }

        // ============================================
        // HELPER: تعيين TenantId عند الإضافة
        // ============================================
        // Todo removet letter
        // private void SetTenantId(T entity)
        // {
        //     var tenantIdProperty = typeof(T).GetProperty("TenantId");
        //     if (tenantIdProperty == null || _currentTenantService.IsSuperAdmin)
        //         return;

        //     var currentValue = tenantIdProperty.GetValue(entity);
        //     if (currentValue == null || (int?)currentValue == 0)
        //     {
        //         tenantIdProperty.SetValue(entity, _currentTenantService.TenantId);
        //     }
        // }

        private void SetTenantId(T entity)
        {
            var tenantIdProperty = typeof(T).GetProperty("TenantId");
            if (tenantIdProperty == null || _currentTenantService.IsSuperAdmin)
                return;

            var currentValue = tenantIdProperty.GetValue(entity);
            if (currentValue == null || (int?)currentValue == 0)
            {
                // تحقق إذا كان Tenant موجود في السياق قبل التعيين
                if (_currentTenantService.HasTenant)
                {
                    tenantIdProperty.SetValue(entity, _currentTenantService.TenantId);
                }
                else
                {
                    // لا يوجد Tenant context، لا تعين شيئاً (أو تعامل مع الحالة كما تريد)
                }
            }
        }

    }
}