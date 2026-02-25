// ============================================
// AuditLogServiceIntegrationTests.cs
// Integration Tests for AuditLogService
// ============================================
// نختبر هنا تفاعل AuditLogService مع DB حقيقية
// IMapper و IHttpContextAccessor يبقيان كـ Mocks
// لأنهما لا علاقة لهما بالـ DB
// ============================================

using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs.AuditLogs;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Repositories;
using ISP.Infrastructure.Services;
using ISP.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ISP.Tests.Integration.Services
{
    public class AuditLogServiceIntegrationTests
    {
        // ============================================
        // Mocks — فقط للخدمات الخارجية
        // ============================================

        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IHttpContextAccessor> _httpContextMock;
        private readonly Mock<ILogger<AuditLogService>> _loggerMock;

        public AuditLogServiceIntegrationTests()
        {
            _mapperMock = new Mock<IMapper>();
            _loggerMock = new Mock<ILogger<AuditLogService>>();
            _httpContextMock = new Mock<IHttpContextAccessor>();

            // HttpContext وهمي مع IP وهمي
            var httpContext = new DefaultHttpContext();
            _httpContextMock.Setup(h => h.HttpContext).Returns(httpContext);
        }

        // ============================================
        // Helper Methods
        // ============================================

        // ينشئ AuditLogService حقيقي مع DB حقيقية
        private AuditLogService CreateService(
            ApplicationDbContext context,
            ICurrentTenantService fakeTenant)
        {
            var unitOfWork = new UnitOfWork(context, fakeTenant);

            return new AuditLogService(
                unitOfWork,
                _mapperMock.Object,
                fakeTenant,
                _httpContextMock.Object,
                _loggerMock.Object
            );
        }

        // ينشئ AuditLog مباشرة في DB
        private async Task<AuditLog> CreateLogAsync(
            ApplicationDbContext context,
            int? tenantId = 1,
            int? userId = 10,
            string username = "ahmed_admin",
            string action = "Create",
            string entityType = "Subscriber",
            int? entityId = 5,
            bool success = true,
            DateTime? timestamp = null)
        {
            var log = new AuditLog
            {
                TenantId = tenantId,
                UserId = userId,
                Username = username,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                IpAddress = "127.0.0.1",
                Timestamp = timestamp ?? DateTime.UtcNow,
                Success = success
            };

            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
            return log;
        }

        // ينشئ Tenant مباشرة في DB
        private async Task<Tenant> CreateTenantAsync(
            ApplicationDbContext context,
            int id,
            string name)
        {
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        }

        // ينشئ AuditLogDto من AuditLog للـ Mapper
        private AuditLogDto MapToDto(AuditLog log) => new AuditLogDto
        {
            Id = log.Id,
            TenantId = log.TenantId,
            UserId = log.UserId,
            Username = log.Username,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            IpAddress = log.IpAddress,
            Timestamp = log.Timestamp,
            Success = log.Success
        };

        // ============================================
        // LogAsync Integration Tests
        // ============================================

        // TEST 1: LogAsync يحفظ السجل في DB حقيقية
        [Fact]
        public async Task LogAsync_ShouldSaveAuditLogToDatabase()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(tenantId: 1, userId: 10, username: "ahmed_admin");
            var service = CreateService(context, fakeTenant);

            // Act
            await service.LogAsync(
                action: "Create",
                entityType: "Subscriber",
                entityId: 5,
                success: true
            );

            // Assert
            // نتحقق مباشرة من DB أن السجل حُفظ
            var savedLog = context.AuditLogs.FirstOrDefault();
            savedLog.Should().NotBeNull();
            savedLog!.Action.Should().Be("Create");
            savedLog.EntityType.Should().Be("Subscriber");
            savedLog.EntityId.Should().Be(5);
            savedLog.TenantId.Should().Be(1);
            savedLog.UserId.Should().Be(10);
            savedLog.Username.Should().Be("ahmed_admin");
            savedLog.Success.Should().BeTrue();
        }

        // TEST 2: LogAsync يحفظ ErrorMessage عند فشل العملية
        [Fact]
        public async Task LogAsync_WithFailure_ShouldSaveErrorMessageToDatabase()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(tenantId: 1, userId: 10, username: "ahmed_admin");
            var service = CreateService(context, fakeTenant);

            // Act
            await service.LogAsync(
                action: "Delete",
                entityType: "User",
                success: false,
                errorMessage: "لا يمكن حذف آخر SuperAdmin"
            );

            // Assert
            var savedLog = context.AuditLogs.FirstOrDefault();
            savedLog.Should().NotBeNull();
            savedLog!.Success.Should().BeFalse();
            savedLog.ErrorMessage.Should().Be("لا يمكن حذف آخر SuperAdmin");
        }

        // ============================================
        // GetAllAsync Integration Tests
        // ============================================

        // TEST 3: فلتر TenantId — يرجع سجلات الـ Tenant المحدد فقط
        [Fact]
        public async Task GetAllAsync_WithTenantFilter_ShouldReturnTenantLogsOnly()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            // سجلان لـ Tenant 1 وسجل لـ Tenant 2
            var log1 = await CreateLogAsync(context, tenantId: 1, action: "Create");
            var log2 = await CreateLogAsync(context, tenantId: 1, action: "Update");
            var log3 = await CreateLogAsync(context, tenantId: 2, action: "Delete");

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            var filter = new AuditLogFilterDto { TenantId = 1, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.TenantId == 1).Should().BeTrue();
        }

        // TEST 4: فلتر Action — يرجع سجلات الـ Action المحدد فقط
        [Fact]
        public async Task GetAllAsync_WithActionFilter_ShouldReturnFilteredLogs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateLogAsync(context, tenantId: 1, action: "Create");
            await CreateLogAsync(context, tenantId: 1, action: "Delete");
            await CreateLogAsync(context, tenantId: 1, action: "Create");

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            var filter = new AuditLogFilterDto { Action = "Create", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.Action == "Create").Should().BeTrue();
        }

        // TEST 5: Paging — يرجع الصفحة الصحيحة
        [Fact]
        public async Task GetAllAsync_WithPaging_ShouldReturnCorrectPage()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            // 5 سجلات في DB
            for (int i = 1; i <= 5; i++)
                await CreateLogAsync(context, tenantId: 1);

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            var filter = new AuditLogFilterDto { PageNumber = 2, PageSize = 2 };

            // Act
            var result = await service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(5);
            result.Items.Should().HaveCount(2);
            result.PageNumber.Should().Be(2);
        }

        // ============================================
        // GetByTenantAsync Integration Tests
        // ============================================

        // TEST 6: يرجع سجلات الـ Tenant المحدد فقط من DB
        [Fact]
        public async Task GetByTenantAsync_ShouldReturnOnlyTenantLogs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateLogAsync(context, tenantId: 1);
            await CreateLogAsync(context, tenantId: 1);
            await CreateLogAsync(context, tenantId: 2); // سجل Tenant آخر

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            // Act
            var result = await service.GetByTenantAsync(tenantId: 1);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.TenantId == 1).Should().BeTrue();
        }

        // ============================================
        // GetByUserAsync Integration Tests
        // ============================================

        // TEST 7: يرجع سجلات المستخدم المحدد فقط من DB
        [Fact]
        public async Task GetByUserAsync_ShouldReturnOnlyUserLogs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateLogAsync(context, userId: 10, username: "ahmed");
            await CreateLogAsync(context, userId: 10, username: "ahmed");
            await CreateLogAsync(context, userId: 99, username: "khalid"); // مستخدم آخر

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            // Act
            var result = await service.GetByUserAsync(userId: 10);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.UserId == 10).Should().BeTrue();
        }

        // ============================================
        // GetByEntityAsync Integration Tests
        // ============================================

        // TEST 8: يرجع سجلات الكيان المحدد فقط من DB
        [Fact]
        public async Task GetByEntityAsync_ShouldReturnOnlyEntityLogs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateLogAsync(context, entityType: "Subscriber", entityId: 5);
            await CreateLogAsync(context, entityType: "Subscriber", entityId: 5);
            await CreateLogAsync(context, entityType: "Plan", entityId: 3); // كيان آخر

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(MapToDto).ToList());

            // Act
            var result = await service.GetByEntityAsync("Subscriber", entityId: 5);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.EntityType == "Subscriber").Should().BeTrue();
        }

        // ============================================
        // CleanupOldLogsAsync Integration Tests
        // ============================================

        // TEST 9: يحذف السجلات القديمة فقط ولا يمس الجديدة
        [Fact]
        public async Task CleanupOldLogsAsync_ShouldDeleteOnlyOldLogs()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            // سجلان قديمان + سجل جديد
            await CreateLogAsync(context, timestamp: DateTime.UtcNow.AddDays(-40));
            await CreateLogAsync(context, timestamp: DateTime.UtcNow.AddDays(-60));
            await CreateLogAsync(context, timestamp: DateTime.UtcNow.AddDays(-5)); // جديد

            // Act
            var deletedCount = await service.CleanupOldLogsAsync(olderThanDays: 30);

            // Assert
            deletedCount.Should().Be(2); // حُذف سجلان فقط

            // السجل الجديد لا يزال في DB
            var remaining = context.AuditLogs.ToList();
            remaining.Should().HaveCount(1);
            remaining.First().Timestamp.Should().BeAfter(DateTime.UtcNow.AddDays(-10));
        }

        // TEST 10: لا يوجد سجلات قديمة — لا يحذف شيئاً
        [Fact]
        public async Task CleanupOldLogsAsync_WithNoOldLogs_ShouldDeleteNothing()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            // سجلان جديدان فقط
            await CreateLogAsync(context, timestamp: DateTime.UtcNow.AddDays(-5));
            await CreateLogAsync(context, timestamp: DateTime.UtcNow.AddDays(-10));

            // Act
            var deletedCount = await service.CleanupOldLogsAsync(olderThanDays: 30);

            // Assert
            deletedCount.Should().Be(0);

            // كلا السجلين لا يزالان في DB
            context.AuditLogs.Count().Should().Be(2);
        }
    }
}