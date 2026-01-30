// ============================================
// IAuditLogService.cs - واجهة خدمة السجلات
// ============================================
using ISP.Application.DTOs;
using ISP.Application.DTOs.AuditLogs;

namespace ISP.Application.Interfaces
{
    public interface IAuditLogService
    {
        // إنشاء سجل جديد
        Task LogAsync(
            string action,
            string entityType,
            int? entityId = null,
            object? oldValues = null,
            object? newValues = null,
            bool success = true,
            string? errorMessage = null
        );

        // عرض السجلات
        Task<PagedResultDto<AuditLogDto>> GetAllAsync(AuditLogFilterDto filter);
        Task<AuditLogDto?> GetByIdAsync(int id);

        // سجلات وكيل معين
        Task<PagedResultDto<AuditLogDto>> GetByTenantAsync(int tenantId, int pageNumber = 1, int pageSize = 10);

        // سجلات مستخدم معين
        Task<PagedResultDto<AuditLogDto>> GetByUserAsync(int userId, int pageNumber = 1, int pageSize = 10);

        // سجلات كيان معين (مثلاً: كل التغييرات على Subscriber رقم 5)
        Task<PagedResultDto<AuditLogDto>> GetByEntityAsync(string entityType, int entityId, int pageNumber = 1, int pageSize = 10);

        // تنظيف السجلات القديمة (أقدم من X يوم)
        Task<int> CleanupOldLogsAsync(int olderThanDays);
    }
}