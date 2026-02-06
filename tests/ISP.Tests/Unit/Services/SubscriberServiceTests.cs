
using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ISP.Tests.Unit.Services
{
    /// <summary>
    /// اختبارات SubscriberService
    /// نختبر منطق إنشاء وقراءة المشتركين
    /// </summary>
    public class SubscriberServiceTests
    {
        // ============================================
        // Dependencies - المعتمديات المزيفة
        // ============================================
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICurrentTenantService> _mockCurrentTenant;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<SubscriberService>> _mockLogger;
        private readonly Mock<IRepository<Subscriber>> _mockSubscriberRepo;
        private readonly Mock<IRepository<Tenant>> _mockTenantRepo;

        // ============================================
        // SUT (System Under Test) - الكود المُختبر
        // ============================================
        private readonly SubscriberService _service;

        /// <summary>
        /// Constructor - يُنفذ قبل كل اختبار
        /// </summary>
        public SubscriberServiceTests()
        {
            // 1. إنشاء Mocks
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCurrentTenant = new Mock<ICurrentTenantService>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<SubscriberService>>();
            _mockSubscriberRepo = new Mock<IRepository<Subscriber>>();
            _mockTenantRepo = new Mock<IRepository<Tenant>>();

            // 2. Setup UnitOfWork ليُرجع Repositories
            _mockUnitOfWork.Setup(x => x.Subscribers).Returns(_mockSubscriberRepo.Object);
            _mockUnitOfWork.Setup(x => x.Tenants).Returns(_mockTenantRepo.Object);

            // 3. Setup CurrentTenant (افتراضياً TenantId = 1)
            _mockCurrentTenant.Setup(x => x.TenantId).Returns(1);
            _mockCurrentTenant.Setup(x => x.IsSuperAdmin).Returns(false);

            // 4. إنشاء Service مع Mocks
            _service = new SubscriberService(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentTenant.Object,
                _mockLogger.Object
            );
        }

        // ============================================
        // TEST 1: إنشاء مشترك - السيناريو الناجح
        // ============================================

        /// <summary>
        /// الاختبار الأول: إنشاء مشترك جديد بنجاح
        /// السيناريو: كل البيانات صحيحة
        /// المتوقع: إرجاع SubscriberResponseDto
        /// </summary>
        [Fact]
        public async Task CreateAsync_ValidDto_ReturnSubsvriberResponse()
        {
            // ============================================
            // Arrange (التحضير)
            // ============================================

            // البيانات المدخلة
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567",
                Email = "ahmad@example.com"
            };

            // Tenant (الوكيل)
            var tenant = new Tenant
            {
                Id = 1,
                Name = "FastNet",
                MaxSubscribers = 100,// الحد الأقصى
                SubscriptionPlan = TenantPlan.Basic
            };

            // Setup: عند طلب Tenant بـ Id = 1
            _mockTenantRepo
                .Setup(t => t.GetByIdAsync(1))
                .ReturnsAsync(tenant);

            // Setup: عدد المشتركين الحاليين = 5
            _mockSubscriberRepo
                .Setup(s => s.CountAsync())
                .ReturnsAsync(5);

            // Setup: لا يوجد تكرار في رقم الهاتف
            _mockSubscriberRepo
                .Setup(s => s.GetAllAsync(It.IsAny<Expression<Func<Subscriber, bool>>>()))
                .ReturnsAsync(new List<Subscriber>());// قائمة فارغة

            // Setup: عند إضافة Subscriber
            _mockSubscriberRepo
                .Setup(s => s.AddAsync(It.IsAny<Subscriber>()))
                .ReturnsAsync((Subscriber s) =>
                {
                    s.Id = 42; // نعطيه Id
                    return s;
                });

            // Setup: SaveChange
            _mockUnitOfWork
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Setup: Mapper
            _mockMapper
            .Setup(m => m.Map<Subscriber>(It.IsAny<CreateSubscriberDto>()))
            .Returns(new Subscriber());

            var responseDto = new SubscriberDto
            {
                Id = 42,
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567",
                Email = "ahmad@example.com",
                Status = "Active"
            };

            _mockMapper
                .Setup(m => m.Map<SubscriberDto>(It.IsAny<Subscriber>()))
                .Returns(responseDto);

            // ============================================
            // Act (التنفيذ)
            // ============================================
            var result = await _service.CreateAsync(dto);

            // ============================================
            // Assert (التحقق)
            // ============================================

            // 1. النتيجة ليست null
            result.Should().NotBeNull();

            // 2. البيانات صحيحة
            result.Id.Should().Be(42);
            result.FullName.Should().Be("أحمد محمد");
            result.PhoneNumber.Should().Be("07801234567");

            // 3. التحقق من استدعاء Methods
            _mockTenantRepo.Verify(t => t.GetByIdAsync(1), Times.Once);
            _mockSubscriberRepo.Verify(s => s.CountAsync(), Times.Once);
            _mockSubscriberRepo.Verify(s => s.AddAsync(It.IsAny<Subscriber>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);

        }

        // ============================================
        // TEST 2: الحد الأقصى للمشتركين
        // ============================================

        /// <summary>
        /// الاختبار الثاني: محاولة إنشاء مشترك عند الوصول للحد الأقصى
        /// السيناريو: المشتركون الحاليون = MaxSubscribers
        /// المتوقع: رمي InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateAsync_ExceedsMaxSubscribers_ThrowsException()
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567"
            };

            var tenant = new Tenant
            {
                Id = 1,
                Name = "FastNet",
                MaxSubscribers = 50,
                SubscriptionPlan = TenantPlan.Free
            };

            _mockTenantRepo
                .Setup(t => t.GetByIdAsync(1))
                .ReturnsAsync(tenant);

            _mockSubscriberRepo
                .Setup(s => s.CountAsync())
                .ReturnsAsync(50);

            // Act & Assert
            // نتوقع رمي Exception
            var act = async () => await _service.CreateAsync(dto);

            await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*للحد الأقصى*"); // يحتوي على "الحد الأقصى"
        }

        // ============================================
        // TEST 3: رقم هاتف مكرر
        // ============================================

        /// <summary>
        /// الاختبار الثالث: محاولة إنشاء مشترك برقم هاتف موجود مسبقاً
        /// السيناريو: رقم الهاتف مُستخدم
        /// المتوقع: رمي InvalidOperationException
        /// </summary>
        [Fact]
        public async Task CreateAsync_DuplicatePhoneNumber_ThrowsException()
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567"
            };

            var tenant = new Tenant
            {
                Id = 1,
                MaxSubscribers = 100
            };

            _mockTenantRepo
                .Setup(t => t.GetByIdAsync(1))
                .ReturnsAsync(tenant);

            _mockSubscriberRepo
                .Setup(s => s.CountAsync())
                .ReturnsAsync(5);

            var existingSubscriber = new Subscriber
            {
                Id = 10,
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567"
            };

            _mockSubscriberRepo
                .Setup(s => s.GetAllAsync(It.IsAny<Expression<Func<Subscriber, bool>>>()))
                .ReturnsAsync(new List<Subscriber> { existingSubscriber });

            // Act & Assert
            var act = async () => await _service.CreateAsync(dto);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*موجود مسبقاً*");
        }

        // ============================================
        // TEST 4: GetByIdAsync - موجود
        // ============================================

        /// <summary>
        /// الاختبار الرابع: جلب مشترك موجود بالـ Id
        /// السيناريو: المشترك موجود
        /// المتوقع: إرجاع SubscriberDto
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsSubscriberDto()
        {
            // Arrange
            var subscriber = new Subscriber
            {
                Id = 5,
                TenantId = 1,
                FullName = "سارة علي",
                PhoneNumber = "07701234567",
                Status = SubscriberStatus.Active
            };

            _mockSubscriberRepo
                .Setup(s => s.GetByIdAsync(5))
                .ReturnsAsync(subscriber);

            var responseDto = new SubscriberDto
            {
                Id = 5,
                FullName = "سارة علي",
                PhoneNumber = "07701234567",
                Status = "Active"
            };

            _mockMapper
            .Setup(m => m.Map<SubscriberDto>(subscriber))
            .Returns(responseDto);

            // Act
            var result = await _service.GetByIdAsync(5);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.FullName.Should().Be("سارة علي");
            result.PhoneNumber.Should().Be("07701234567");
        }

        // ============================================
        // TEST 5: GetByIdAsync - غير موجود
        // ============================================

        /// <summary>
        /// الاختبار الخامس: جلب مشترك غير موجود
        /// السيناريو: Id غير موجود
        /// المتوقع: إرجاع null
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_NonExistingId_ReturnsNull()
        {
            // Arrange
            _mockSubscriberRepo
                .Setup(x => x.GetByIdAsync(999))
                .ReturnsAsync((Subscriber?)null); // غير موجود

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }
    }
}