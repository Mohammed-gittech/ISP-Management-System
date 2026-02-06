
using FluentAssertions;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ISP.Tests.Integration.Repositories
{
    /// <summary>
    /// Integration Tests للـ Repository
    /// نختبر التكامل مع DbContext باستخدام In-Memory Database
    /// </summary>
    public class SubscriberRepositoryTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly GenericRepository<Subscriber> _repository;
        private readonly TestCurrentTenantService _currentTenant;

        public SubscriberRepositoryTests()
        {
            // 1. إنشاء In-Memory Database بـ GUID عشوائي
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // 2. إنشاء CurrentTenantService وهمي
            _currentTenant = new TestCurrentTenantService();
            _currentTenant.SetTenant(1);// TenantId = 1

            // 3. إنشاء DbContext
            _context = new ApplicationDbContext(options, _currentTenant);

            // 4. إنشاء Repository
            _repository = new GenericRepository<Subscriber>(_context, _currentTenant);

            // 5. تحضير بيانات أولية
            SeedData();
        }

        /// <summary>
        /// تحضير بيانات وهمية للاختبارات
        /// </summary>
        private void SeedData()
        {
            // Tenant 1 - شركتنا
            var tenant1 = new Tenant
            {
                Id = 1,
                Name = "FastNet",
                MaxSubscribers = 100,
                SubscriptionPlan = TenantPlan.Basic,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Tenant 2 - شركة أخرى
            var tenant2 = new Tenant
            {
                Id = 2,
                Name = "SpeedNet",
                MaxSubscribers = 50,
                SubscriptionPlan = TenantPlan.Free,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tenants.AddRange(tenant1, tenant2);

            // مشتركو Tenant 1
            var subscriber1 = new List<Subscriber>
            {
                new Subscriber
                {
                    Id = 1,
                    TenantId = 1,
                    FullName = "أحمد محمد",
                    PhoneNumber = "07801234567",
                    Status = SubscriberStatus.Active,
                    RegistrationDate = DateTime.UtcNow.AddDays(-30)
                },
                new Subscriber
                {
                    Id = 2,
                    TenantId = 1,
                    FullName = "سارة علي",
                    PhoneNumber = "07701234567",
                    Status = SubscriberStatus.Active,
                    RegistrationDate = DateTime.UtcNow.AddDays(-20)
                },
                new Subscriber
                {
                    Id = 3,
                    TenantId = 1,
                    FullName = "محمد حسن",
                    PhoneNumber = "07501234567",
                    Status = SubscriberStatus.Inactive,
                    RegistrationDate = DateTime.UtcNow.AddDays(-10),
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            // مشتركو Tenant 2
            var subscriber2 = new List<Subscriber>
            {
                new Subscriber
                {
                    Id = 4,
                    TenantId = 2,
                    FullName = "خالد كريم",
                    PhoneNumber = "07601234567",
                    Status = SubscriberStatus.Active,
                    RegistrationDate = DateTime.UtcNow.AddDays(-15)
                }
            };

            _context.Subscribers.AddRange(subscriber1);
            _context.Subscribers.AddRange(subscriber2);
            _context.SaveChanges();

            // تنظيف ChangeTracker
            _context.ChangeTracker.Clear();
        }

        // ============================================
        // TEST 1: Multi-Tenancy Filtering
        // ============================================
        [Fact]
        public async Task GetAllAsync_ReturnOnlyCurrentTenantSubscribers()
        {
            // Arrange
            _currentTenant.SetTenant(1);

            // Act
            var result = await _repository.GetAllAsync();

            // Assert

            // يجب أن يرجع 2 فقط (أحمد و سارة)
            // لا يرجع محمد (محذوف) ولا خالد (Tenant آخر)
            result.Should().HaveCount(2);
            result.Should().OnlyContain(s => s.TenantId == 1);
            result.Should().OnlyContain(s => !s.IsDeleted);

            result.Should().Contain(s => s.FullName == "أحمد محمد");
            result.Should().Contain(s => s.FullName == "سارة علي");
        }

        // ============================================
        // TEST 2: GetByIdAsync - Same Tenant
        // ============================================
        [Fact]
        public async Task GetByIdAsync_ExistingIdSameTenant_ReturnsSubscriber()
        {
            // Arrange
            _currentTenant.SetTenant(1);

            // Act
            var result = await _repository.GetByIdAsync(1);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            result.FullName.Should().Be("أحمد محمد");
            result.PhoneNumber.Should().Be("07801234567");
            result.TenantId.Should().Be(1);
        }

        // ============================================
        // TEST 3: GetByIdAsync - Different Tenant
        // ============================================
        [Fact]
        public async Task GetByIdAsync_DifferentTenant_ReturnsNull()
        {
            // Arrange
            _currentTenant.SetTenant(1);

            // Act 
            var result = await _repository.GetByIdAsync(4);

            // Assert
            result.Should().BeNull();
        }

        // ============================================
        // TEST 4: AddAsync
        // ============================================
        [Fact]
        public async Task AddAsync_ValidSubscriber_AddsToDatabase()
        {
            // Arrange
            _currentTenant.SetTenant(1);

            var newSubscriber = new Subscriber
            {
                FullName = "علي حسين",
                PhoneNumber = "07301234567",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow
            };

            // Act 
            var result = await _repository.AddAsync(newSubscriber);
            await _context.SaveChangesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.TenantId.Should().Be(1);

            // التحقق من Database
            _context.ChangeTracker.Clear();
            var fromDb = await _context.Subscribers.FindAsync(result.Id);
            fromDb.Should().NotBeNull();
            fromDb!.FullName.Should().Be("علي حسين");
            fromDb.TenantId.Should().Be(1);
        }

        // ============================================
        // TEST 5: CountAsync
        // ============================================

        [Fact]
        public async Task CountAsync_ReturnsActiveCount()
        {
            // Arrange
            _currentTenant.SetTenant(1);

            // Act
            var count = await _repository.CountAsync();

            // Assert
            count.Should().Be(2); // أحمد و سارة فقط (محمد محذوف)
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }


    // ============================================
    // Helper: TestCurrentTenantService (مُصحح)
    // ============================================

    /// <summary>
    /// CurrentTenantService وهمي للاختبارات
    /// يطابق ICurrentTenantService الحقيقي
    /// </summary>
    public class TestCurrentTenantService : ICurrentTenantService
    {
        private int? _tenantId;
        private bool _isSuperAdmin;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// TenantId الحالي (int وليس int?)
        /// يرمي Exception إذا لم يُعيّن (نفس السلوك الحقيقي)
        /// </summary>
        public int TenantId
        {
            get
            {
                if (_tenantId == null)
                    throw new InvalidOperationException("Tenant context not set.");
                return _tenantId.Value;
            }
        }

        public bool IsSuperAdmin => _isSuperAdmin;

        // اختياري في الاختبارات
        public int? UserId => null;
        public string? Username => null;
        public bool HasTenant => _tenantId != null;

        // ============================================
        // Methods
        // ============================================

        public void SetTenant(int tenantId)
        {
            _tenantId = tenantId;
            _isSuperAdmin = false;
        }

        public void SetSuperAdmin()
        {
            _isSuperAdmin = true;
            _tenantId = null;
        }
    }
}