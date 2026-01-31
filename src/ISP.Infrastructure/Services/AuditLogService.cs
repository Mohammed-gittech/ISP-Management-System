// ============================================
// AuditLogService.cs - تنفيذ خدمة السجلات
// ============================================
using System.Text.Json;
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
                    TenantId = _currentTenantService.TenantId,
                    UserId = _currentTenantService.UserId,
                    Username = _currentTenantService.Username ?? "Anonymous",

                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,

                    OldValues = FormatJsonValue(oldValues),
                    NewValues = FormatJsonValue(newValues),

                    IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),

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

        // ============================================
        // 2. GET ALL (مع Filtering)
        // ============================================
        public async Task<PagedResultDto<AuditLogDto>> GetAllAsync(AuditLogFilterDto filter)
        {
            var allLogs = await _unitOfWork.AuditLogs.GetAllAsync();

            // تطبيق الفلاتر
            if (filter.TenantId.HasValue)
                allLogs = allLogs.Where(a => a.TenantId == filter.TenantId.Value);

            if (filter.UserId.HasValue)
                allLogs = allLogs.Where(a => a.UserId == filter.UserId.Value);

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
            {
                allLogs = allLogs.Where(a =>
                    a.Username.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    a.IpAddress.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = allLogs.Count();

            var logs = allLogs
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

            var totalCount = tenantLogs.Count();

            var logs = tenantLogs
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

            var totalCount = userLogs.Count();

            var logs = userLogs
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

            var totalCount = entityLogs.Count();

            var logs = entityLogs
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

            foreach (var log in oldLogs)
            {
                await _unitOfWork.AuditLogs.DeleteAsync(log);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Count} audit logs older than {Days} days", oldLogs.Count(), olderThanDays);

            return oldLogs.Count();
        }

    }
}