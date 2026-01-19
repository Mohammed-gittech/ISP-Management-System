
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
        /// Constructor - يستقبل DbContext من DI
        /// </summary>
        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // ============================================
        // Get Methods
        // ============================================

        /// <summary>
        /// الحصول على Entity بالـ Id
        /// Note: Query Filters تُطبق تلقائياً (TenantId)
        /// </summary>
        public async Task<T?> GetByIdAsync(int id)
        {
            // FindAsync تبحث بالـ Primary Key
            // لكن لا تطبق Query Filters!
            // لذلك نستخدم FirstOrDefaultAsync
            return await _dbSet
                .Where(e => EF.Property<int>(e, "Id") == id)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// الحصول على كل Entities
        /// Note: Query Filters تُطبق تلقائياً
        /// </summary>
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();
        }

        // <summary>
        /// التحقق من وجود Entity
        /// </summary>
        public async Task<bool> ExistsAsync(int id)
        {
            return await _dbSet
                .Where(e => EF.Property<int>(e, "Id") == id)
                .AnyAsync();
        }

        // ============================================
        // Create, Update, Delete
        // ============================================

        /// <summary>
        /// إضافة Entity جديد
        /// Note: لا نعمل SaveChanges هنا - يتم في UnitOfWork
        /// </summary>
        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }

        /// <summary>
        /// تحديث Entity موجود
        /// </summary>
        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// حذف Entity
        /// </summary>
        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }
    }
}