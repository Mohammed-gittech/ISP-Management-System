using System.Linq.Expressions;

namespace ISP.Domain.Interfaces
{
    /// <summary>
    /// Generic Repository Interface
    /// يوفر CRUD Operations + Multi-Tenancy + Soft Delete
    /// </summary>
    /// <typeparam name="T">Entity Type</typeparam>
    public interface IRepository<T> where T : class
    {
        // ============================================
        // Basic CRUD Operations
        // ============================================

        /// <summary>
        /// الحصول على Entity بالـ Id
        /// ✅ يطبق Multi-Tenancy Filter تلقائياً
        /// ✅ يطبق Soft Delete Filter تلقائياً (IsDeleted = 0)
        /// </summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// الحصول على كل Entities
        /// ✅ يطبق Multi-Tenancy Filter تلقائياً
        /// ✅ يطبق Soft Delete Filter تلقائياً
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// الحصول على Entities بشرط معين
        /// ✅ يطبق Multi-Tenancy Filter تلقائياً
        /// ✅ يطبق Soft Delete Filter تلقائياً
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// إضافة Entity جديد
        /// ✅ يعين TenantId تلقائياً
        /// </summary>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// تحديث Entity
        /// </summary>
        Task UpdateAsync(T entity);

        /// <summary>
        /// حذف Entity نهائياً من Database
        /// ⚠️ استخدم SoftDeleteAsync بدلاً منه
        /// </summary>
        Task DeleteAsync(T entity);

        /// <summary>
        /// التحقق من وجود Entity
        /// ✅ يطبق Multi-Tenancy + Soft Delete Filters
        /// </summary>
        Task<bool> ExistsAsync(int id);

        // ============================================
        // Multi-Tenancy Support
        // ============================================

        /// <summary>
        /// الحصول على كل Entities لـ Tenant معين
        /// </summary>
        Task<IEnumerable<T>> GetByTenantAsync(int tenantId);

        /// <summary>
        /// الحصول على Entities لـ Tenant معين بشرط
        /// </summary>
        Task<IEnumerable<T>> GetByTenantAsync(int tenantId, Expression<Func<T, bool>> predicate);

        // ============================================
        // Count Operations
        // ============================================

        /// <summary>
        /// عد كل Entities
        /// ✅ يطبق Multi-Tenancy + Soft Delete Filters
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// عد Entities بشرط
        /// ✅ يطبق Multi-Tenancy + Soft Delete Filters
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);

        // ============================================
        // SOFT DELETE OPERATIONS (جديد)
        // ============================================

        /// <summary>
        /// حذف ناعم (Soft Delete)
        /// يضع علامة IsDeleted = true بدلاً من الحذف النهائي
        /// يعين DeletedAt = DateTime.UtcNow
        /// ✅ الطريقة الموصى بها للحذف
        /// </summary>
        /// <param name="entity">Entity المراد حذفه</param>
        Task SoftDeleteAsync(T entity);

        /// <summary>
        /// حذف ناعم بالـ Id
        /// </summary>
        /// <param name="id">Id للـ Entity</param>
        /// <returns>true إذا تم الحذف، false إذا لم يُعثر عليه</returns>
        Task<bool> SoftDeleteByIdAsync(int id);

        /// <summary>
        /// استرجاع Entity محذوف (Restore)
        /// يضع IsDeleted = false
        /// يضع DeletedAt = null
        /// </summary>
        /// <param name="entity">Entity المراد استرجاعه</param>
        Task RestoreAsync(T entity);

        /// <summary>
        /// استرجاع Entity محذوف بالـ Id
        /// </summary>
        /// <param name="id">Id للـ Entity</param>
        /// <returns>true إذا تم الاسترجاع، false إذا لم يُعثر عليه</returns>
        Task<bool> RestoreByIdAsync(int id);

        /// <summary>
        /// الحصول على كل Entities المحذوفة (IsDeleted = 1)
        /// ✅ يطبق Multi-Tenancy Filter
        /// ❌ لا يطبق Soft Delete Filter (يظهر المحذوفات)
        /// </summary>
        Task<IEnumerable<T>> GetDeletedAsync();

        /// <summary>
        /// الحصول على Entities المحذوفة بشرط
        /// </summary>
        Task<IEnumerable<T>> GetDeletedAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// الحصول على كل Entities (نشطة + محذوفة)
        /// ⚠️ يتجاهل Soft Delete Filter
        /// ✅ يطبق Multi-Tenancy Filter
        /// </summary>
        Task<IEnumerable<T>> GetAllIncludingDeletedAsync();

        /// <summary>
        /// الحصول على Entity بالـ Id (بما فيهم المحذوفات)
        /// ⚠️ يتجاهل Soft Delete Filter
        /// ✅ يطبق Multi-Tenancy Filter
        /// </summary>
        Task<T?> GetByIdIncludingDeletedAsync(int id);

        // ============================================
        // RETENTION & CLEANUP
        // ============================================

        /// <summary>
        /// الحصول على Entities المحذوفة قبل تاريخ معين
        /// للاستخدام في Retention Cleanup Job
        /// </summary>
        /// <param name="beforeDate">التاريخ الحد</param>
        /// <returns>Entities محذوفة قبل هذا التاريخ</returns>
        Task<IEnumerable<T>> GetDeletedBeforeDateAsync(DateTime beforeDate);

        /// <summary>
        /// حذف نهائي (Permanent Delete) لـ Entities المحذوفة قبل تاريخ معين
        /// ⚠️ SuperAdmin only
        /// ⚠️ لا يمكن التراجع
        /// </summary>
        /// <param name="beforeDate">التاريخ الحد</param>
        /// <returns>عدد Entities المحذوفة نهائياً</returns>
        Task<int> PermanentDeleteOldAsync(DateTime beforeDate);
    }
}