// ============================================
// AuditLogServiceTests.cs
// Unit Tests for AuditLogService
// ============================================

using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs;
using ISP.Application.DTOs.AuditLogs;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace ISP.Tests.Unit.Services
{
    public class AuditLogServiceTests
    {
        // ============================================
        // Mocks
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ICurrentTenantService> _currentTenantMock;
        private readonly Mock<IHttpContextAccessor> _httpContextMock;
        private readonly Mock<ILogger<AuditLogService>> _loggerMock;
        private readonly Mock<IRepository<AuditLog>> _auditRepoMock;
        private readonly Mock<IRepository<Tenant>> _tenantRepoMock;

        // SUT
        private readonly AuditLogService _service;

        // ثوابت
        private const int TenantId = 1;
        private const int UserId = 10;
        private const int LogId = 100;

        // ============================================
        // Constructor
        // ============================================

        public AuditLogServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _currentTenantMock = new Mock<ICurrentTenantService>();
            _httpContextMock = new Mock<IHttpContextAccessor>();
            _loggerMock = new Mock<ILogger<AuditLogService>>();
            _auditRepoMock = new Mock<IRepository<AuditLog>>();
            _tenantRepoMock = new Mock<IRepository<Tenant>>();

            // ربط الـ Repositories بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.AuditLogs).Returns(_auditRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);

            // إعداد المستخدم الحالي
            _currentTenantMock.Setup(c => c.TenantId).Returns(TenantId);
            _currentTenantMock.Setup(c => c.UserId).Returns(UserId);
            _currentTenantMock.Setup(c => c.Username).Returns("ahmed_admin");

            // إعداد HttpContext وهمي مع IP وهمي
            var httpContext = new DefaultHttpContext();
            _httpContextMock.Setup(h => h.HttpContext).Returns(httpContext);

            _service = new AuditLogService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _currentTenantMock.Object,
                _httpContextMock.Object,
                _loggerMock.Object
            );
        }

        // ============================================
        // Helper Methods
        // ============================================

        // ينشئ AuditLog وهمي
        private AuditLog CreateFakeLog(
            int id = LogId,
            int? tenantId = TenantId,
            string action = "Create",
            string entityType = "Subscriber",
            bool success = true,
            DateTime? timestamp = null) => new AuditLog
            {
                Id = id,
                TenantId = tenantId,
                UserId = UserId,
                Username = "ahmed_admin",
                Action = action,
                EntityType = entityType,
                EntityId = 5,
                IpAddress = "127.0.0.1",
                Timestamp = timestamp ?? DateTime.UtcNow,
                Success = success
            };

        // ينشئ AuditLogDto وهمي
        private AuditLogDto CreateFakeLogDto(
            int id = LogId,
            int? tenantId = TenantId,
            string action = "Create",
            string entityType = "Subscriber",
            bool success = true) => new AuditLogDto
            {
                Id = id,
                TenantId = tenantId,
                UserId = UserId,
                Username = "ahmed_admin",
                Action = action,
                EntityType = entityType,
                EntityId = 5,
                IpAddress = "127.0.0.1",
                Timestamp = DateTime.UtcNow,
                Success = success
            };

        // ============================================
        // LogAsync Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة — يجب أن ينشئ السجل ويحفظ
        [Fact]
        [Trait("Category", "Service")]
        public async Task LogAsync_WithValidData_ShouldCreateAuditLog()
        {
            // Arrange
            _auditRepoMock
                .Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync((AuditLog a) => a);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.LogAsync(
                action: "Create",
                entityType: "Subscriber",
                entityId: 5,
                newValues: new { Name = "محمد" },
                success: true
            );

            // Assert
            // نتأكد أن AddAsync استُدعي مع السجل الصحيح
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
                a.Action == "Create" &&
                a.EntityType == "Subscriber" &&
                a.EntityId == 5 &&
                a.TenantId == TenantId &&
                a.UserId == UserId &&
                a.Username == "ahmed_admin" &&
                a.Success == true
            )), Times.Once);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: تسجيل عملية فاشلة — يجب أن يحفظ ErrorMessage
        [Fact]
        [Trait("Category", "Service")]
        public async Task LogAsync_WithFailedOperation_ShouldSaveErrorMessage()
        {
            // Arrange
            _auditRepoMock
                .Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync((AuditLog a) => a);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.LogAsync(
                action: "Delete",
                entityType: "User",
                success: false,
                errorMessage: "لا يمكن حذف آخر SuperAdmin"
            );

            // Assert
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
                a.Success == false &&
                a.ErrorMessage == "لا يمكن حذف آخر SuperAdmin"
            )), Times.Once);
        }

        // الاختبار الثالث: فشل الحفظ — يجب أن لا يعطل النظام
        [Fact]
        [Trait("Category", "Service")]
        public async Task LogAsync_WhenSaveFails_ShouldNotThrowException()
        {
            // Arrange
            // نجعل AddAsync يرمي استثناء لمحاكاة فشل الحفظ
            _auditRepoMock
                .Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
                .ThrowsAsync(new Exception("خطأ في قاعدة البيانات"));

            // Act
            // لا يجب أن يرمي استثناء — الـ try/catch في LogAsync يمنع ذلك
            var act = async () => await _service.LogAsync("Create", "Subscriber");

            // Assert
            await act.Should().NotThrowAsync();

            // تسجيل الخطأ في الـ Logger يجب أن يحدث
            _loggerMock.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ), Times.Once);
        }

        // الاختبار الرابع: OldValues و NewValues كـ Object — يجب أن يحوّلهم لـ JSON
        [Fact]
        [Trait("Category", "Service")]
        public async Task LogAsync_WithObjectValues_ShouldSerializeToJson()
        {
            // Arrange
            var oldValues = new { Name = "خالد", IsActive = true };
            var newValues = new { Name = "محمد", IsActive = false };

            _auditRepoMock
                .Setup(r => r.AddAsync(It.IsAny<AuditLog>()))
                .ReturnsAsync((AuditLog a) => a);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.LogAsync(
                action: "Update",
                entityType: "Subscriber",
                oldValues: oldValues,
                newValues: newValues
            );

            // Assert
            // نتأكد أن القيم تحوّلت لـ JSON وليست null
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(a =>
                a.OldValues != null &&
                a.NewValues != null
            )), Times.Once);
        }

        // ============================================
        // GetAllAsync Tests
        // ============================================

        // الاختبار الأول: بدون فلتر — يجب أن يرجع كل السجلات مع Paging
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithNoFilter_ShouldReturnPagedResult()
        {
            // Arrange
            var logs = new List<AuditLog>
            {
                CreateFakeLog(id: 1),
                CreateFakeLog(id: 2),
                CreateFakeLog(id: 3)
            };

            var logDtos = logs.Select(l => CreateFakeLogDto(id: l.Id)).ToList();

            _auditRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(logs);

            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns(logDtos);

            var filter = new AuditLogFilterDto { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(filter);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(3);
            result.TotalCount.Should().Be(3);
            result.PageNumber.Should().Be(1);
        }

        // الاختبار الثاني: فلتر Action — يجب أن يرجع سجلات الـ Action المحدد فقط
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithActionFilter_ShouldReturnFilteredLogs()
        {
            // Arrange
            var logs = new List<AuditLog>
            {
                CreateFakeLog(id: 1, action: "Create"),
                CreateFakeLog(id: 2, action: "Delete"),
                CreateFakeLog(id: 3, action: "Create")
            };

            _auditRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(logs);

            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // الـ Mapper يرجع فقط الـ Create logs
            var createLogs = logs.Where(l => l.Action == "Create").ToList();
            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns(createLogs.Select(l => CreateFakeLogDto(id: l.Id, action: "Create")).ToList());

            var filter = new AuditLogFilterDto { Action = "Create", PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(2); // فقط Create
            result.Items.All(l => l.Action == "Create").Should().BeTrue();
        }

        // الاختبار الثالث: فلتر Success = false — يجب أن يرجع العمليات الفاشلة فقط
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithSuccessFilter_ShouldReturnFailedLogsOnly()
        {
            // Arrange
            var logs = new List<AuditLog>
            {
                CreateFakeLog(id: 1, success: true),
                CreateFakeLog(id: 2, success: false),
                CreateFakeLog(id: 3, success: false)
            };

            _auditRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(logs);

            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            var failedLogs = logs.Where(l => !l.Success).ToList();
            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns(failedLogs.Select(l => CreateFakeLogDto(id: l.Id, success: false)).ToList());

            var filter = new AuditLogFilterDto { Success = false, PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.Success == false).Should().BeTrue();
        }

        // الاختبار الرابع: Paging — يجب أن يرجع الصفحة الصحيحة
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithPaging_ShouldReturnCorrectPage()
        {
            // Arrange — 5 سجلات، PageSize = 2، نطلب الصفحة الثانية
            var logs = Enumerable.Range(1, 5)
                .Select(i => CreateFakeLog(id: i))
                .ToList();

            _auditRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(logs);

            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // الصفحة الثانية تحتوي على سجلَين فقط
            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(x => CreateFakeLogDto(id: x.Id)).ToList());

            var filter = new AuditLogFilterDto { PageNumber = 2, PageSize = 2 };

            // Act
            var result = await _service.GetAllAsync(filter);

            // Assert
            result.TotalCount.Should().Be(5);   // الكل = 5
            result.Items.Should().HaveCount(2);  // الصفحة الثانية = 2
            result.PageNumber.Should().Be(2);
        }

        // ============================================
        // GetByIdAsync Tests
        // ============================================

        // الاختبار الأول: سجل موجود — يجب أن يرجع AuditLogDto مع TenantName
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenLogExists_ShouldReturnDtoWithTenantName()
        {
            // Arrange
            var log = CreateFakeLog();
            var logDto = CreateFakeLogDto();

            _auditRepoMock
                .Setup(r => r.GetByIdAsync(LogId))
                .ReturnsAsync(log);

            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            _mapperMock
                .Setup(m => m.Map<AuditLogDto>(log))
                .Returns(logDto);

            // Act
            var result = await _service.GetByIdAsync(LogId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(LogId);
            result.TenantName.Should().Be("شركة النور");
        }

        // الاختبار الثاني: سجل غير موجود — يجب أن يرجع null
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenLogNotFound_ShouldReturnNull()
        {
            // Arrange
            _auditRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((AuditLog?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();

            // لا يجب أن يستدعي Tenants
            _tenantRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ============================================
        // GetByTenantAsync Tests
        // ============================================

        // الاختبار الأول: يرجع سجلات الـ Tenant المحدد مع Paging
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByTenantAsync_ShouldReturnTenantLogsWithPaging()
        {
            // Arrange
            var logs = Enumerable.Range(1, 5)
                .Select(i => CreateFakeLog(id: i, tenantId: TenantId))
                .ToList();

            _auditRepoMock
                .Setup(r => r.GetByTenantAsync(TenantId))
                .ReturnsAsync(logs);

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(x => CreateFakeLogDto(id: x.Id)).ToList());

            // Act
            var result = await _service.GetByTenantAsync(TenantId, pageNumber: 1, pageSize: 3);

            // Assert
            result.TotalCount.Should().Be(5);  // الكل = 5
            result.Items.Should().HaveCount(3); // الصفحة الأولى = 3
            result.PageNumber.Should().Be(1);

            _auditRepoMock.Verify(r => r.GetByTenantAsync(TenantId), Times.Once);
        }

        // الاختبار الثاني: Tenant بدون سجلات — يرجع قائمة فارغة
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByTenantAsync_WithNoLogs_ShouldReturnEmptyResult()
        {
            // Arrange
            _auditRepoMock
                .Setup(r => r.GetByTenantAsync(TenantId))
                .ReturnsAsync(new List<AuditLog>());

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns(new List<AuditLogDto>());

            // Act
            var result = await _service.GetByTenantAsync(TenantId);

            // Assert
            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        // ============================================
        // GetByUserAsync Tests
        // ============================================

        // الاختبار الأول: يرجع سجلات المستخدم المحدد مع Paging
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByUserAsync_ShouldReturnUserLogsWithPaging()
        {
            // Arrange
            var logs = Enumerable.Range(1, 4)
                .Select(i => CreateFakeLog(id: i))
                .ToList();

            _auditRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                .ReturnsAsync(logs);

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(x => CreateFakeLogDto(id: x.Id)).ToList());

            // Act
            var result = await _service.GetByUserAsync(UserId, pageNumber: 1, pageSize: 2);

            // Assert
            result.TotalCount.Should().Be(4);  // الكل = 4
            result.Items.Should().HaveCount(2); // الصفحة الأولى = 2
        }

        // ============================================
        // GetByEntityAsync Tests
        // ============================================

        // الاختبار الأول: يرجع سجلات الكيان المحدد
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByEntityAsync_ShouldReturnEntityLogs()
        {
            // Arrange — كل سجلات Subscriber رقم 5
            var logs = new List<AuditLog>
            {
                CreateFakeLog(id: 1, entityType: "Subscriber"),
                CreateFakeLog(id: 2, entityType: "Subscriber"),
            };

            _auditRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                .ReturnsAsync(logs);

            _mapperMock
                .Setup(m => m.Map<List<AuditLogDto>>(It.IsAny<List<AuditLog>>()))
                .Returns((List<AuditLog> l) => l.Select(x => CreateFakeLogDto(id: x.Id, entityType: "Subscriber")).ToList());

            // Act
            var result = await _service.GetByEntityAsync("Subscriber", entityId: 5);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.All(l => l.EntityType == "Subscriber").Should().BeTrue();
        }

        // ============================================
        // CleanupOldLogsAsync Tests
        // ============================================

        // الاختبار الأول: يحذف السجلات القديمة فقط ويرجع العدد الصحيح
        [Fact]
        [Trait("Category", "Service")]
        public async Task CleanupOldLogsAsync_ShouldDeleteOldLogsAndReturnCount()
        {
            // Arrange — سجلان قديمان (أقدم من 30 يوم)
            var oldLogs = new List<AuditLog>
            {
                CreateFakeLog(id: 1, timestamp: DateTime.UtcNow.AddDays(-40)),
                CreateFakeLog(id: 2, timestamp: DateTime.UtcNow.AddDays(-60))
            };

            _auditRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                .ReturnsAsync(oldLogs);

            _auditRepoMock
                .Setup(r => r.DeleteAsync(It.IsAny<AuditLog>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _service.CleanupOldLogsAsync(olderThanDays: 30);

            // Assert
            result.Should().Be(2); // حُذف سجلان

            _auditRepoMock.Verify(r => r.DeleteAsync(It.IsAny<AuditLog>()), Times.Exactly(2));
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: لا يوجد سجلات قديمة — يرجع 0
        [Fact]
        [Trait("Category", "Service")]
        public async Task CleanupOldLogsAsync_WithNoOldLogs_ShouldReturnZero()
        {
            // Arrange — لا يوجد سجلات أقدم من 30 يوم
            _auditRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<AuditLog, bool>>>()))
                .ReturnsAsync(new List<AuditLog>());

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _service.CleanupOldLogsAsync(olderThanDays: 30);

            // Assert
            result.Should().Be(0);

            // لا يجب أن يُستدعى DeleteAsync
            _auditRepoMock.Verify(r => r.DeleteAsync(It.IsAny<AuditLog>()), Times.Never);
        }



    }
}