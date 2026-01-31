
using System.Linq.Expressions;

namespace ISP.Domain.Interfaces
{
    public interface IRepository<T> where T : class
    {
        // Basic CRUD
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<bool> ExistsAsync(int id);

        // Multi-Tenancy Support
        Task<IEnumerable<T>> GetByTenantAsync(int tenantId);
        Task<IEnumerable<T>> GetByTenantAsync(int tenantId, Expression<Func<T, bool>> predicate);

        // Count
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    }
}