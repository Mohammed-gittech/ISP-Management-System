// ============================================
// AuditLogsController.cs - إدارة سجلات العمليات
// ============================================
using ISP.Application.DTOs.AuditLogs;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuditLogsController : BaseController
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(
            IAuditLogService auditLogService,
            IAuthorizationService authorizationService)
            : base(authorizationService)
        {
            _auditLogService = auditLogService;
        }

        // ============================================
        // 1. GET ALL LOGS (SuperAdmin only)
        // ============================================
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll([FromQuery] AuditLogFilterDto filter)
        {
            var result = await _auditLogService.GetAllAsync(filter);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 2. GET LOG BY ID
        // ============================================
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById(int id)
        {
            var log = await _auditLogService.GetByIdAsync(id);
            if (log == null)
                return NotFound(new { success = false, message = "السجل غير موجود" });

            // TenantAdmin يرى سجلات وكيله فقط
            var forbid = await CheckOwnershipAsync(log);
            if (forbid != null) return forbid;

            return Ok(new { success = true, data = log });
        }

        // ============================================
        // 3. GET LOGS BY TENANT
        // ============================================
        [HttpGet("tenant/{tenantId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetByTenant(
            int tenantId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            // TenantAdmin يرى وكيله فقط
            if (IsCrossTenantAccess(tenantId))
                return Forbid();

            var result = await _auditLogService.GetByTenantAsync(tenantId, page, pageSize);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 4. GET LOGS BY USER
        // ============================================
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByUser(
            int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _auditLogService.GetByUserAsync(userId, page, pageSize);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 5. GET LOGS BY ENTITY
        // ============================================
        [HttpGet("entity/{entityType}/{entityId}")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetByEntity(
            string entityType,
            int entityId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _auditLogService.GetByEntityAsync(entityType, entityId, page, pageSize);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 6. SEARCH LOGS (Advanced Filtering)
        // ============================================
        [HttpPost("search")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Search([FromBody] AuditLogFilterDto filter)
        {
            // TenantAdmin يبحث في سجلات وكيله فقط
            if (!IsSuperAdmin())
                filter.TenantId = GetCurrentTenantId();

            var result = await _auditLogService.GetAllAsync(filter);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 7. GET MY LOGS (Current User)
        // ============================================
        [HttpGet("my-logs")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var currentUserId = GetCurrentUserId();

            var result = await _auditLogService.GetByUserAsync(currentUserId, page, pageSize);
            return Ok(new { success = true, data = result });
        }

        // ============================================
        // 8. CLEANUP OLD LOGS (SuperAdmin only)
        // ============================================
        [HttpDelete("cleanup/{olderThanDays}")]
        [Authorize(Roles = "SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CleanupOldLogs(int olderThanDays)
        {
            if (olderThanDays < 30)
                return BadRequest(new { success = false, message = "يجب أن يكون الحد الأدنى 30 يوم" });

            var deletedCount = await _auditLogService.CleanupOldLogsAsync(olderThanDays);
            return Ok(new
            {
                success = true,
                message = $"تم حذف {deletedCount} سجل",
                deletedCount
            });
        }

        // ============================================
        // 9. GET STATISTICS
        // ============================================
        [HttpGet("statistics")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<IActionResult> GetStatistics([FromQuery] int? tenantId = null)
        {
            // Fetch all records without pagination for accurate statistics
            var filter = new AuditLogFilterDto
            {
                TenantId = tenantId,
                PageNumber = 1,
                PageSize = int.MaxValue // ← جلب كل السجلات
            };

            var allLogs = await _auditLogService.GetAllAsync(filter);

            var stats = new
            {
                totalLogs = allLogs.TotalCount,
                successfulOperations = allLogs.Items.Count(l => l.Success),
                failedOperations = allLogs.Items.Count(l => !l.Success),

                actionBreakdown = allLogs.Items
                    .GroupBy(l => l.Action)
                    .Select(g => new { action = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList(),

                entityBreakdown = allLogs.Items
                    .GroupBy(l => l.EntityType)
                    .Select(g => new { entityType = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList(),

                topUsers = allLogs.Items
                    .GroupBy(l => l.Username)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { username = g.Key, operations = g.Count() })
                    .ToList()
            };

            return Ok(new { success = true, data = stats });
        }
    }
}