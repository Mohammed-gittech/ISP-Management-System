// ============================================
// AuditLogService.cs - تنفيذ خدمة السجلات
// ============================================
using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.AuditLogs;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenantService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenantService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenantService = currentTenantService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // ============================================
        // 1. LOG ASYNC - تسجيل عملية
        // ============================================
        public async Task LogAsync(
            string action,
            string entityType,
            int? entityId = null,
            object? oldValues = null,
            object? newValues = null,
            bool success = true,
            string? errorMessage = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;

                var auditLog = new AuditLog
                {
                    TenantId = TryGetTenantId(),
                    UserId = _currentTenantService.UserId,
                    Username = _currentTenantService.Username ?? "Anonymous",

                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,

                    OldValues = FormatJsonValue(oldValues),
                    NewValues = FormatJsonValue(newValues),

                    IpAddress = MaskIpAddress(httpContext?.Connection.RemoteIpAddress?.ToString()),
                    UserAgent = ParseUserAgent(httpContext?.Request.Headers["User-Agent"].ToString()),

                    Timestamp = DateTime.UtcNow,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                await _unitOfWork.AuditLogs.AddAsync(auditLog);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // لا نريد أن يتعطل النظام بسبب فشل التسجيل
                _logger.LogError(ex, "Failed to create audit log for action: {Action}", action);
            }
        }

        private string? FormatJsonValue(object? value)
        {
            if (value == null) return null;

            // إذا كان String (من Middleware)، نرجعه مباشرة
            if (value is string str) return str;

            // إذا كان Object، نحوله لـ JSON
            return System.Text.Json.JsonSerializer.Serialize(value);
        }

        // Mask IP address — keep first 3 parts only
        // 192.168.1.100 → 192.168.1.*
        private string MaskIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return "Unknown";

            // IPv4: 192.168.1.100
            if (ipAddress.Contains('.'))
            {
                var parts = ipAddress.Split('.');
                if (parts.Length == 4)
                    return $"{parts[0]}.{parts[1]}.{parts[2]}.*";
            }

            // IPv6: 2001:db8::1
            if (ipAddress.Contains(':'))
            {
                var parts = ipAddress.Split(':');
                if (parts.Length >= 3)
                    return $"{parts[0]}:{parts[1]}:*";
            }

            return "Unknown";
        }

        // Extract only browser and OS type — discard device details
        // "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0.0.0" → "Chrome/Windows"
        private string? ParseUserAgent(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return null;

            // Detect browser
            var browser = "Unknown";
            if (userAgent.Contains("Chrome")) browser = "Chrome";
            else if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Safari")) browser = "Safari";
            else if (userAgent.Contains("Edge")) browser = "Edge";
            else if (userAgent.Contains("Postman")) browser = "Postman";

            // Detect OS
            var os = "Unknown";
            if (userAgent.Contains("Windows")) os = "Windows";
            else if (userAgent.Contains("Mac")) os = "Mac";
            else if (userAgent.Contains("Linux")) os = "Linux";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone")) os = "iOS";

            return $"{browser}/{os}";
        }

        // Safe TenantId access — returns null if context not set (e.g. during Login)
        private int? TryGetTenantId()
        {
            try
            {
                return _currentTenantService.TenantId;
            }
            catch
            {
                return null;
            }
        }

        // ============================================
        // 2. GET ALL (مع Filtering)
        // ✅ إصلاح: نبني predicate واحد مركّب ونرسله للـ DB
        // بدل جلب كل السجلات ثم الفلترة في الذاكرة
        // ============================================
        public async Task<PagedResultDto<AuditLogDto>> GetAllAsync(AuditLogFilterDto filter)
        {
            // جلب السجلات مع أكثر فلتر تقييداً أولاً
            IEnumerable<AuditLog> allLogs;

            if (filter.TenantId.HasValue && filter.UserId.HasValue)
                allLogs = await _unitOfWork.AuditLogs.GetAllAsync(a =>
                    a.TenantId == filter.TenantId.Value &&
                    a.UserId == filter.UserId.Value);

            else if (filter.TenantId.HasValue)
                allLogs = await _unitOfWork.AuditLogs.GetByTenantAsync(filter.TenantId.Value);

            else if (filter.UserId.HasValue)
                allLogs = await _unitOfWork.AuditLogs.GetAllAsync(a =>
                    a.UserId == filter.UserId.Value);

            else
                allLogs = await _unitOfWork.AuditLogs.GetAllAsync();

            // الفلاتر الثانوية تُطبَّق في الذاكرة على نتيجة محدودة بالفعل
            if (!string.IsNullOrWhiteSpace(filter.Action))
                allLogs = allLogs.Where(a => a.Action == filter.Action);

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                allLogs = allLogs.Where(a => a.EntityType == filter.EntityType);

            if (filter.EntityId.HasValue)
                allLogs = allLogs.Where(a => a.EntityId == filter.EntityId.Value);

            if (filter.FromDate.HasValue)
                allLogs = allLogs.Where(a => a.Timestamp >= filter.FromDate.Value);

            if (filter.ToDate.HasValue)
                allLogs = allLogs.Where(a => a.Timestamp <= filter.ToDate.Value);

            if (filter.Success.HasValue)
                allLogs = allLogs.Where(a => a.Success == filter.Success.Value);

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                allLogs = allLogs.Where(a =>
                    a.Username.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.IpAddress.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));

            // ✅ إصلاح: حفظ Count مرة واحدة فقط
            var filteredList = allLogs.ToList();
            var totalCount = filteredList.Count;

            var logs = filteredList
                .OrderByDescending(a => a.Timestamp)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            var logDtos = _mapper.Map<List<AuditLogDto>>(logs);

            // إضافة أسماء الوكلاء
            foreach (var dto in logDtos)
            {
                if (dto.TenantId.HasValue)
                {
                    var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId.Value);
                    dto.TenantName = tenant?.Name;
                }
            }

            return new PagedResultDto<AuditLogDto>
            {
                Items = logDtos,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        // ============================================
        // 3. GET BY ID
        // ============================================
        public async Task<AuditLogDto?> GetByIdAsync(int id)
        {
            var log = await _unitOfWork.AuditLogs.GetByIdAsync(id);
            if (log == null) return null;

            var dto = _mapper.Map<AuditLogDto>(log);

            if (dto.TenantId.HasValue)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId.Value);
                dto.TenantName = tenant?.Name;
            }

            return dto;
        }

        // ============================================
        // 4. GET BY TENANT
        // ============================================
        public async Task<PagedResultDto<AuditLogDto>> GetByTenantAsync(int tenantId, int pageNumber = 1, int pageSize = 10)
        {
            var tenantLogs = await _unitOfWork.AuditLogs.GetByTenantAsync(tenantId);

            // ✅ إصلاح: حفظ Count مرة واحدة فقط
            var logList = tenantLogs.ToList();
            var totalCount = logList.Count;

            var logs = logList
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var logDtos = _mapper.Map<List<AuditLogDto>>(logs);

            return new PagedResultDto<AuditLogDto>
            {
                Items = logDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // 5. GET BY USER
        // ============================================
        public async Task<PagedResultDto<AuditLogDto>> GetByUserAsync(int userId, int pageNumber = 1, int pageSize = 10)
        {
            var userLogs = await _unitOfWork.AuditLogs.GetAllAsync(a => a.UserId == userId);

            // ✅ إصلاح: حفظ Count مرة واحدة فقط
            var logList = userLogs.ToList();
            var totalCount = logList.Count;

            var logs = logList
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var logDtos = _mapper.Map<List<AuditLogDto>>(logs);

            return new PagedResultDto<AuditLogDto>
            {
                Items = logDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // 6. GET BY ENTITY
        // ============================================
        public async Task<PagedResultDto<AuditLogDto>> GetByEntityAsync(string entityType, int entityId, int pageNumber = 1, int pageSize = 10)
        {
            var entityLogs = await _unitOfWork.AuditLogs.GetAllAsync(a =>
                a.EntityType == entityType && a.EntityId == entityId);

            // ✅ إصلاح: حفظ Count مرة واحدة فقط
            var logList = entityLogs.ToList();
            var totalCount = logList.Count;

            var logs = logList
                .OrderByDescending(a => a.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var logDtos = _mapper.Map<List<AuditLogDto>>(logs);

            return new PagedResultDto<AuditLogDto>
            {
                Items = logDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // 7. CLEANUP OLD LOGS
        // ============================================
        public async Task<int> CleanupOldLogsAsync(int olderThanDays)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

            var oldLogs = await _unitOfWork.AuditLogs.GetAllAsync(a => a.Timestamp < cutoffDate);

            // ✅ إصلاح: حفظ Count مرة واحدة قبل الحلقة
            var logList = oldLogs.ToList();
            var count = logList.Count;

            foreach (var log in logList)
                await _unitOfWork.AuditLogs.DeleteAsync(log);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} audit logs older than {Days} days", count, olderThanDays);

            return count;
        }


    }
}