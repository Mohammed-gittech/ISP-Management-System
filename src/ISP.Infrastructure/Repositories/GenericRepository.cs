using System.Linq.Expressions;
using ISP.Application.Interfaces;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISP.Infrastructure.Repositories
{
    /// <summary>
    /// Generic Repository - CRUD عام لكل Entities
    /// ✅ Multi-Tenancy Support
    /// ✅ Soft Delete Support
    /// ✅ Expression Tree Filtering
    /// </summary>
    public class GenericRepository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;
        private readonly ICurrentTenantService _currentTenantService;

        public GenericRepository(ApplicationDbContext context, ICurrentTenantService currentTenantService)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _currentTenantService = currentTenantService;
        }

        // ============================================
        // BASIC CRUD OPERATIONS
        // ============================================

        public async Task<T?> GetByIdAsync(int id)
        {
            var query = _dbSet.Where(e => EF.Property<int>(e, "Id") == id);
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.ToListAsync();
        }

        public async Task<T> AddAsync(T entity)
        {
            SetTenantId(entity);
            await _dbSet.AddAsync(entity);
            return entity;
        }

        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            var query = _dbSet.Where(e => EF.Property<int>(e, "Id") == id);
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.AnyAsync();
        }

        // ============================================
        // MULTI-TENANCY SUPPORT
        // ============================================

        public async Task<IEnumerable<T>> GetByTenantAsync(int tenantId)
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query, tenantId);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> GetByTenantAsync(int tenantId, Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query, tenantId);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.ToListAsync();
        }

        // ============================================
        // COUNT OPERATIONS
        // ============================================

        public async Task<int> CountAsync()
        {
            var query = _dbSet.AsQueryable();
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.CountAsync();
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            var query = _dbSet.Where(predicate);
            query = ApplyTenantFilter(query);
            // Global Query Filter يطبق Soft Delete تلقائياً
            return await query.CountAsync();
        }

        // ============================================
        // SOFT DELETE OPERATIONS
        // ============================================

        public async Task SoftDeleteAsync(T entity)
        {
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            var deletedAtProperty = typeof(T).GetProperty("DeletedAt");

            if (isDeletedProperty != null)
                isDeletedProperty.SetValue(entity, true);

            if (deletedAtProperty != null)
                deletedAtProperty.SetValue(entity, DateTime.UtcNow);

            await UpdateAsync(entity);
        }

        public async Task<bool> SoftDeleteByIdAsync(int id)
        {
            // ✅ استخدم IgnoreQueryFilters للحصول على Entity حتى لو محذوف
            var entity = await GetByIdIncludingDeletedAsync(id);

            if (entity == null)
                return false;

            await SoftDeleteAsync(entity);
            return true;
        }

        public async Task RestoreAsync(T entity)
        {
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            var deletedAtProperty = typeof(T).GetProperty("DeletedAt");

            if (isDeletedProperty != null)
                isDeletedProperty.SetValue(entity, false);

            if (deletedAtProperty != null)
                deletedAtProperty.SetValue(entity, null);

            await UpdateAsync(entity);
        }

        public async Task<bool> RestoreByIdAsync(int id)
        {
            // ✅ نحتاج IgnoreQueryFilters للحصول على المحذوفات
            var entity = await GetByIdIncludingDeletedAsync(id);

            if (entity == null)
                return false;

            // التحقق من أنه محذوف فعلاً
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            if (isDeletedProperty != null)
            {
                var isDeleted = (bool?)isDeletedProperty.GetValue(entity);
                if (isDeleted != true)
                    return false; // ليس محذوفاً أصلاً
            }

            await RestoreAsync(entity);
            return true;
        }

        public async Task<IEnumerable<T>> GetDeletedAsync()
        {
            var query = _dbSet.IgnoreQueryFilters(); // ✅ تجاهل Global Filter
            query = ApplyTenantFilter(query);

            // فلترة يدوية: IsDeleted = true
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            if (isDeletedProperty != null)
            {
                var parameter = Expression.Parameter(typeof(T), "e");
                var property = Expression.Property(parameter, isDeletedProperty);
                var constant = Expression.Constant(true);
                var equals = Expression.Equal(property, constant);
                var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

                query = query.Where(lambda);
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> GetDeletedAsync(Expression<Func<T, bool>> predicate)
        {
            var deleted = await GetDeletedAsync();
            return deleted.AsQueryable().Where(predicate);
        }

        public async Task<IEnumerable<T>> GetAllIncludingDeletedAsync()
        {
            var query = _dbSet.IgnoreQueryFilters(); // ✅ تجاهل Global Filter
            query = ApplyTenantFilter(query);
            return await query.ToListAsync();
        }

        public async Task<T?> GetByIdIncludingDeletedAsync(int id)
        {
            var query = _dbSet.IgnoreQueryFilters() // ✅ تجاهل Global Filter
                .Where(e => EF.Property<int>(e, "Id") == id);
            query = ApplyTenantFilter(query);
            return await query.FirstOrDefaultAsync();
        }

        // ============================================
        // RETENTION & CLEANUP
        // ============================================

        public async Task<IEnumerable<T>> GetDeletedBeforeDateAsync(DateTime beforeDate)
        {
            var query = _dbSet.IgnoreQueryFilters();
            query = ApplyTenantFilter(query);

            // فلترة: IsDeleted = true AND DeletedAt < beforeDate
            var isDeletedProperty = typeof(T).GetProperty("IsDeleted");
            var deletedAtProperty = typeof(T).GetProperty("DeletedAt");

            if (isDeletedProperty != null && deletedAtProperty != null)
            {
                var parameter = Expression.Parameter(typeof(T), "e");

                // IsDeleted == true
                var isDeletedProp = Expression.Property(parameter, isDeletedProperty);
                var isDeletedTrue = Expression.Equal(isDeletedProp, Expression.Constant(true));

                // DeletedAt < beforeDate
                var deletedAtProp = Expression.Property(parameter, deletedAtProperty);
                var deletedAtValue = Expression.Property(deletedAtProp, "Value"); // DateTime? → DateTime
                var beforeDateConst = Expression.Constant(beforeDate, typeof(DateTime));
                var deletedBefore = Expression.LessThan(deletedAtValue, beforeDateConst);

                // AND
                var combined = Expression.AndAlso(isDeletedTrue, deletedBefore);
                var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);

                query = query.Where(lambda);
            }

            return await query.ToListAsync();
        }

        public async Task<int> PermanentDeleteOldAsync(DateTime beforeDate)
        {
            var entitiesToDelete = await GetDeletedBeforeDateAsync(beforeDate);
            var count = entitiesToDelete.Count();

            foreach (var entity in entitiesToDelete)
            {
                await DeleteAsync(entity); // حذف نهائي
            }

            return count;
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// تطبيق Multi-Tenancy Filter
        /// يستخدم Expression Trees لبناء Filter ديناميكي
        /// </summary>
        private IQueryable<T> ApplyTenantFilter(IQueryable<T> query, int? specificTenantId = null)
        {
            var tenantIdProperty = typeof(T).GetProperty("TenantId");
            if (tenantIdProperty == null)
                return query; // Entity بدون TenantId

            // SuperAdmin يرى كل شيء (إلا إذا حددنا TenantId معين)
            if (_currentTenantService.IsSuperAdmin && specificTenantId == null)
                return query;

            // تحديد TenantId المطلوب
            int? targetTenantId = specificTenantId;

            if (!targetTenantId.HasValue)
            {
                if (_currentTenantService.HasTenant)
                    targetTenantId = _currentTenantService.TenantId;
            }

            if (!targetTenantId.HasValue)
                return query; // لا يوجد Tenant Context

            // بناء Expression: entity => entity.TenantId == targetTenantId
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

        /// <summary>
        /// تعيين TenantId عند الإضافة
        /// </summary>
        private void SetTenantId(T entity)
        {
            var tenantIdProperty = typeof(T).GetProperty("TenantId");
            if (tenantIdProperty == null || _currentTenantService.IsSuperAdmin)
                return;

            var currentValue = tenantIdProperty.GetValue(entity);
            if (currentValue == null || (int?)currentValue == 0)
            {
                if (_currentTenantService.HasTenant)
                {
                    tenantIdProperty.SetValue(entity, _currentTenantService.TenantId);
                }
            }
        }
    }
}