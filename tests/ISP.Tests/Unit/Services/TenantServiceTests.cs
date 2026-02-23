// ============================================
// TenantServiceTests.cs
// Unit tests for TenantService
// ============================================

using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure;
using Moq;

namespace ISP.Tests.Unit.Services
{
    public class TenantServiceTests
    {
        // ============================================
        // Mocks — نسخ وهمية بدل قاعدة البيانات الحقيقية
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IRepository<Tenant>> _tenantRepoMock;
        private readonly Mock<IRepository<TenantSubscription>> _subscriptionRepoMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IRepository<Subscriber>> _subscriberRepoMock;
        private readonly Mock<IRepository<TenantPayment>> _tenantPaymentRepoMock;

        // SUT — الكود الحقيقي الذي نختبره
        private readonly TenantService _service;

        // ثابت لتجنب تكرار الرقم في كل اختبار
        private const int TenantId = 1;

        // ============================================
        // Constructor — يُنفَّذ تلقائياً قبل كل اختبار
        // ============================================

        public TenantServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _tenantRepoMock = new Mock<IRepository<Tenant>>();
            _subscriptionRepoMock = new Mock<IRepository<TenantSubscription>>();
            _userRepoMock = new Mock<IRepository<User>>();
            _subscriberRepoMock = new Mock<IRepository<Subscriber>>();
            _tenantPaymentRepoMock = new Mock<IRepository<TenantPayment>>();

            // ربط كل Repository بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.TenantSubscriptions).Returns(_subscriptionRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Subscribers).Returns(_subscriberRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.TenantPayments).Returns(_tenantPaymentRepoMock.Object);

            // إنشاء الـ Service الحقيقي بالـ Mocks
            _service = new TenantService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _passwordHasherMock.Object
            );
        }

        // ============================================
        // Helper Methods — بيانات وهمية جاهزة للاستخدام
        // ============================================

        // ينشئ Tenant وهمي — plan تتحكم في الباقة
        private Tenant CreateFakeTenant(int id = TenantId, TenantPlan plan = TenantPlan.Free) => new Tenant
        {
            Id = id,
            Name = "شركة النور",
            ContactEmail = "info@alnoor.com",
            ContactPhone = "07801234567",
            SubscriptionPlan = plan,
            MaxSubscribers = plan == TenantPlan.Free ? 50 : plan == TenantPlan.Basic ? 500 : int.MaxValue,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Subscribers = new List<Subscriber>()
        };

        // ينشئ TenantSubscription وهمي — status تتحكم في الحالة
        private TenantSubscription CreateFakeSubscription(
            int id = 1,
            TenantSubscriptionStatus status = TenantSubscriptionStatus.Active,
            TenantPlan plan = TenantPlan.Free) => new TenantSubscription
            {
                Id = id,
                TenantId = TenantId,
                Plan = plan,
                Price = plan == TenantPlan.Free ? 0 : plan == TenantPlan.Basic ? 29 : 99,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                Status = status
            };

        // ينشئ CreateTenantDto وهمي — plan تتحكم في الباقة
        private CreateTenantDto CreateFakeCreateDto(TenantPlan plan = TenantPlan.Free) => new CreateTenantDto
        {
            Name = "شركة النور",
            ContactEmail = "info@alnoor.com",
            ContactPhone = "07801234567",
            SubscriptionPlan = plan,
            DurationMonths = 1,
            AdminUsername = "admin_alnoor",
            AdminEmail = "admin@alnoor.com",
            AdminPassword = "Admin@123"
        };

        // ينشئ TenantDto وهمي للرد
        private TenantDto CreateFakeTenantDto(int id = TenantId, bool isActive = true, string plan = "Free") => new TenantDto
        {
            Id = id,
            Name = "شركة النور",
            ContactEmail = "info@alnoor.com",
            IsActive = isActive,
            SubscriptionPlan = plan,
            MaxSubscribers = 50
        };

        // ============================================
        // CreateAsync Tests
        // ============================================

        // الاختبار الأول: باقة Free — يجب أن يُنشئ Tenant مفعَّل فوراً
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithFreePlan_ShouldReturnActiveTenantDto()
        {
            // Arrange
            var dto = CreateFakeCreateDto(TenantPlan.Free);
            var tenant = CreateFakeTenant(plan: TenantPlan.Free);
            var expectedDto = CreateFakeTenantDto(isActive: true, plan: "Free");

            // لا يوجد Email مكرر — قائمة فارغة تعني عدم وجود تكرار
            _tenantRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(new List<Tenant>());

            // الـ Mapper يحول Dto إلى Tenant Entity
            _mapperMock
                .Setup(m => m.Map<Tenant>(dto))
                .Returns(tenant);

            // عند إضافة Tenant نعطيه Id = 1
            _tenantRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Tenant>()))
                .ReturnsAsync((Tenant t) => { t.Id = TenantId; return t; });

            // عند إضافة Subscription نرجعه كما هو
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) => s);

            // عند إضافة User نرجعه كما هو
            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            // تشفير كلمة المرور — نرجع نصاً وهمياً بدل BCrypt الحقيقي
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.AdminPassword))
                .Returns("hashed_password_123");

            // حفظ التغييرات — نتظاهر بأن صفاً واحداً تم حفظه
            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // الـ Mapper يحول Tenant → TenantDto للرد النهائي
            _mapperMock
                .Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>()))
                .Returns(expectedDto);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeTrue();           // Free تُفعَّل فوراً
            result.SubscriptionPlan.Should().Be("Free"); // الباقة صحيحة

            // نتأكد أن كل الخطوات الثلاث تمت: Tenant + Subscription + User
            _tenantRepoMock.Verify(r => r.AddAsync(It.IsAny<Tenant>()), Times.Once);
            _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<TenantSubscription>()), Times.Once);
            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
            _passwordHasherMock.Verify(p => p.HashPassword(dto.AdminPassword), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: باقة Basic — يجب أن يُنشئ Tenant غير مفعَّل ينتظر الدفع
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithBasicPlan_ShouldReturnInactiveTenantDto()
        {
            // Arrange
            var dto = CreateFakeCreateDto(TenantPlan.Basic);
            dto.DurationMonths = 3; // 3 أشهر × 29$ = 87$

            var tenant = CreateFakeTenant(plan: TenantPlan.Basic);
            tenant.IsActive = false; // Basic لا تُفعَّل حتى يتم الدفع
            var expectedDto = CreateFakeTenantDto(isActive: false, plan: "Basic");

            _tenantRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(new List<Tenant>());

            _mapperMock.Setup(m => m.Map<Tenant>(dto)).Returns(tenant);

            _tenantRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Tenant>()))
                .ReturnsAsync((Tenant t) => { t.Id = TenantId; return t; });

            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) => s);

            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.AdminPassword))
                .Returns("hashed_password_123");

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>()))
                .Returns(expectedDto);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.IsActive.Should().BeFalse();           // Basic لا تُفعَّل حتى يتم الدفع
            result.SubscriptionPlan.Should().Be("Basic"); // الباقة صحيحة
        }

        // الاختبار الثالث: Email مكرر — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithDuplicateEmail_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();

            // نرجع Tenant موجود لمحاكاة تكرار الـ Email
            _tenantRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(new List<Tenant> { CreateFakeTenant() });

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*البريد الإلكتروني موجود مسبقاً*");
        }

        // الاختبار الرابع: حساب السعر الصحيح لـ Basic Plan
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithBasicPlan3Months_ShouldCreateSubscriptionWithCorrectPrice()
        {
            // Arrange
            var dto = CreateFakeCreateDto(TenantPlan.Basic);
            dto.DurationMonths = 3; // المتوقع: 29 × 3 = 87$

            var tenant = CreateFakeTenant(plan: TenantPlan.Basic);

            _tenantRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(new List<Tenant>());

            _mapperMock.Setup(m => m.Map<Tenant>(dto)).Returns(tenant);

            _tenantRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Tenant>()))
                .ReturnsAsync((Tenant t) => { t.Id = TenantId; return t; });

            // نلتقط الـ Subscription الذي يُضاف لنتحقق من سعره
            TenantSubscription? capturedSubscription = null;
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) =>
                {
                    capturedSubscription = s; // نحفظه للفحص لاحقاً
                    return s;
                });

            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => u);

            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.AdminPassword))
                .Returns("hashed_password_123");

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>()))
                .Returns(CreateFakeTenantDto(plan: "Basic"));

            // Act
            await _service.CreateAsync(dto);

            // Assert
            capturedSubscription.Should().NotBeNull();
            capturedSubscription!.Price.Should().Be(87);                               // 29 × 3 = 87$
            capturedSubscription.Status.Should().Be(TenantSubscriptionStatus.Pending); // ينتظر الدفع
        }

        // الاختبار الخامس: Admin User يُنشأ مع Password مشفر
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_ShouldCreateAdminUserWithHashedPassword()
        {
            // Arrange
            var dto = CreateFakeCreateDto();
            var tenant = CreateFakeTenant();

            _tenantRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(new List<Tenant>());

            _mapperMock.Setup(m => m.Map<Tenant>(dto)).Returns(tenant);

            _tenantRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Tenant>()))
                .ReturnsAsync((Tenant t) => { t.Id = TenantId; return t; });

            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) => s);

            // نلتقط الـ User الذي يُضاف لنتحقق من بياناته
            User? capturedUser = null;
            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => { capturedUser = u; return u; });

            _passwordHasherMock
                .Setup(p => p.HashPassword("Admin@123"))
                .Returns("$2a$12$hashedValue"); // نرجع hash وهمي

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>()))
                .Returns(CreateFakeTenantDto());

            // Act
            await _service.CreateAsync(dto);

            // Assert
            capturedUser.Should().NotBeNull();
            capturedUser!.PasswordHash.Should().Be("$2a$12$hashedValue"); // كلمة المرور مشفرة
            capturedUser.Role.Should().Be(UserRole.TenantAdmin);           // دور صحيح
            capturedUser.Username.Should().Be(dto.AdminUsername);          // اسم المستخدم صحيح

            _passwordHasherMock.Verify(p => p.HashPassword("Admin@123"), Times.Once);
        }

        // ============================================
        // GetByIdAsync Tests
        // ============================================

        // الاختبار الأول: Tenant موجود — يجب أن يرجع TenantDto
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenTenantExists_ShouldReturnTenantDto()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            var expectedDto = CreateFakeTenantDto();

            // عندما يطلب Tenant بـ Id = 1، نرجع الـ Tenant الوهمي
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(tenant);

            _mapperMock
                .Setup(m => m.Map<TenantDto>(tenant))
                .Returns(expectedDto);

            // Act
            var result = await _service.GetByIdAsync(TenantId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(TenantId);
            result.Name.Should().Be("شركة النور");
        }

        // الاختبار الثاني: Tenant غير موجود — يجب أن يرجع null
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenTenantNotFound_ShouldReturnNull()
        {
            // Arrange
            // نرجع null لمحاكاة عدم وجود Tenant بهذا الـ Id
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Tenant?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        // ============================================
        // GetAllAsync Tests
        // ============================================

        // الاختبار الأول: يرجع نتيجة مقسمة بشكل صحيح
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_ShouldReturnCorrectPagedResult()
        {
            // Arrange
            // ننشئ 15 Tenant وهمي لاختبار الـ Pagination
            var tenants = Enumerable.Range(1, 15)
                .Select(i => CreateFakeTenant(id: i))
                .ToList();

            _tenantRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(tenants);

            _mapperMock
                .Setup(m => m.Map<List<TenantDto>>(It.IsAny<object>()))
                .Returns(new List<TenantDto>());

            // Act
            var result = await _service.GetAllAsync(pageNumber: 1, pageSize: 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(15); // المجموع الكلي 15
            result.PageNumber.Should().Be(1);  // الصفحة الأولى
            result.PageSize.Should().Be(10);   // 10 عناصر في الصفحة
        }

        // الاختبار الثاني: الصفحة الثانية
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithPage2_ShouldReturnCorrectPaginationData()
        {
            // Arrange
            var tenants = Enumerable.Range(1, 15)
                .Select(i => CreateFakeTenant(id: i))
                .ToList();

            _tenantRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(tenants);

            _mapperMock
                .Setup(m => m.Map<List<TenantDto>>(It.IsAny<object>()))
                .Returns(new List<TenantDto>());

            // Act
            var result = await _service.GetAllAsync(pageNumber: 2, pageSize: 10);

            // Assert
            result.PageNumber.Should().Be(2); // نحن في الصفحة الثانية
        }

        // ============================================
        // UpdateAsync Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة — يجب أن يحدّث ويحفظ
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WithValidData_ShouldUpdateAndSave()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            var dto = new UpdateTenantDto
            {
                Name = "شركة النور المحدثة",
                ContactEmail = "new@alnoor.com",
                ContactPhone = "07809999999"
            };

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);
            _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.UpdateAsync(TenantId, dto);

            // Assert
            tenant.Name.Should().Be("شركة النور المحدثة");   // تحقق أن الاسم تغيّر
            tenant.ContactEmail.Should().Be("new@alnoor.com"); // تحقق أن الإيميل تغيّر

            _tenantRepoMock.Verify(r => r.UpdateAsync(tenant), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: Tenant غير موجود — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WhenTenantNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Tenant?)null);

            var dto = new UpdateTenantDto { Name = "اسم جديد" };

            // Act
            var act = async () => await _service.UpdateAsync(999, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الوكيل غير موجود*");
        }

        // الاختبار الثالث: الحقول الفارغة لا تُحدَّث
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WithNullFields_ShouldNotOverwriteExistingValues()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            var originalEmail = tenant.ContactEmail; // "info@alnoor.com"

            // dto لا يحتوي على Email — يجب أن يبقى كما هو
            var dto = new UpdateTenantDto { Name = "اسم جديد فقط" };

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);
            _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.UpdateAsync(TenantId, dto);

            // Assert
            tenant.Name.Should().Be("اسم جديد فقط");       // الاسم تغيّر
            tenant.ContactEmail.Should().Be(originalEmail); // الإيميل لم يتغيّر
        }

        // ============================================
        // DeactivateAsync Tests
        // ============================================

        // الاختبار الأول: Tenant موجود — يجب أن يُعطِّل ويرجع true
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeactivateAsync_WhenTenantExists_ShouldDeactivateAndReturnTrue()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            tenant.IsActive = true; // نبدأ بـ Tenant مفعَّل

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);
            _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.DeactivateAsync(TenantId);

            // Assert
            result.Should().BeTrue();            // عملية ناجحة
            tenant.IsActive.Should().BeFalse(); // تأكد أن IsActive أصبح false

            _tenantRepoMock.Verify(r => r.UpdateAsync(tenant), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: Tenant غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeactivateAsync_WhenTenantNotFound_ShouldReturnFalse()
        {
            // Arrange
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Tenant?)null);

            // Act
            var result = await _service.DeactivateAsync(999);

            // Assert
            result.Should().BeFalse();

            // لا يجب أن يُستدعى Update إذا لم يُعثَر على الـ Tenant
            _tenantRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>()), Times.Never);
        }

        // ============================================
        // ActivateAsync Tests
        // ============================================

        // الاختبار الأول: Tenant موجود — يجب أن يُفعِّل ويرجع true
        [Fact]
        [Trait("Category", "Service")]
        public async Task ActivateAsync_WhenTenantExists_ShouldActivateAndReturnTrue()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            tenant.IsActive = false; // نبدأ بـ Tenant معطَّل

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);
            _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.ActivateAsync(TenantId);

            // Assert
            result.Should().BeTrue();           // عملية ناجحة
            tenant.IsActive.Should().BeTrue(); // تأكد أن IsActive أصبح true

            _tenantRepoMock.Verify(r => r.UpdateAsync(tenant), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: Tenant غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ActivateAsync_WhenTenantNotFound_ShouldReturnFalse()
        {
            // Arrange
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Tenant?)null);

            // Act
            var result = await _service.ActivateAsync(999);

            // Assert
            result.Should().BeFalse();

            _tenantRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>()), Times.Never);
        }

        // ============================================
        // GetCurrentSubscribersCountAsync Tests
        // ============================================

        // الاختبار الأول: يرجع العدد الصحيح للمشتركين
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetCurrentSubscribersCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            // نتظاهر بأن هناك 25 مشترك لهذا الـ Tenant
            _subscriberRepoMock
                .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Subscriber, bool>>>()))
                .ReturnsAsync(25);

            // Act
            var result = await _service.GetCurrentSubscribersCountAsync(TenantId);

            // Assert
            result.Should().Be(25); // العدد مطابق لما أرجعه الـ Mock
        }

        // ============================================
        // CanAddSubscriberAsync Tests
        // ============================================

        // الاختبار الأول: المشتركون أقل من الحد — يمكن إضافة مشترك
        [Fact]
        [Trait("Category", "Service")]
        public async Task CanAddSubscriberAsync_WhenBelowLimit_ShouldReturnTrue()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            tenant.MaxSubscribers = 50;
            tenant.IsActive = true;

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);

            // 30 مشترك من أصل 50 — لا يزال هناك مجال
            _subscriberRepoMock
                .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Subscriber, bool>>>()))
                .ReturnsAsync(30);

            // Act
            var result = await _service.CanAddSubscriberAsync(TenantId);

            // Assert
            result.Should().BeTrue(); // 30 < 50 — يمكن إضافة مشترك
        }

        // الاختبار الثاني: المشتركون وصلوا للحد الأقصى — لا يمكن إضافة مشترك
        [Fact]
        [Trait("Category", "Service")]
        public async Task CanAddSubscriberAsync_WhenAtMaxLimit_ShouldReturnFalse()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            tenant.MaxSubscribers = 50;
            tenant.IsActive = true;

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);

            // وصل للحد الأقصى — لا يمكن إضافة مشترك جديد
            _subscriberRepoMock
                .Setup(r => r.CountAsync(It.IsAny<Expression<Func<Subscriber, bool>>>()))
                .ReturnsAsync(50);

            // Act
            var result = await _service.CanAddSubscriberAsync(TenantId);

            // Assert
            result.Should().BeFalse(); // 50 >= 50 — الحد الأقصى وصل
        }

        // الاختبار الثالث: Tenant معطَّل — لا يمكن إضافة مشترك
        [Fact]
        [Trait("Category", "Service")]
        public async Task CanAddSubscriberAsync_WhenTenantIsInactive_ShouldReturnFalse()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            tenant.IsActive = false; // الـ Tenant معطَّل

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);

            // Act
            var result = await _service.CanAddSubscriberAsync(TenantId);

            // Assert
            result.Should().BeFalse(); // Tenant معطَّل — لا يمكن إضافة مشترك
        }

        // ============================================
        // RenewRequestAsync Tests
        // ============================================

        // الاختبار الأول: طلب تجديد صحيح — يجب أن ينشئ Subscription بـ Pending
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewRequestAsync_WithValidRequest_ShouldCreatePendingSubscription()
        {
            // Arrange
            var tenant = CreateFakeTenant();
            var dto = new RenewTenantSubscriptionDto
            {
                Plan = TenantPlan.Basic,
                DurationMonths = 3
            };

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);

            // لا يوجد طلب معلق مسبقاً — قائمة فارغة
            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TenantSubscription, bool>>>()))
                .ReturnsAsync(new List<TenantSubscription>());

            // نلتقط الـ Subscription الذي يُضاف لنتحقق من حالته
            TenantSubscription? capturedSubscription = null;
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) =>
                {
                    capturedSubscription = s;
                    s.Id = 10;
                    return s;
                });

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<TenantSubscriptionDto>(It.IsAny<TenantSubscription>()))
                .Returns(new TenantSubscriptionDto { Id = 10, Status = "Pending" });

            // Act
            var result = await _service.RenewRequestAsync(TenantId, dto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("Pending"); // يجب أن يكون في حالة انتظار

            capturedSubscription.Should().NotBeNull();
            capturedSubscription!.Status.Should().Be(TenantSubscriptionStatus.Pending); // حالة الانتظار
            capturedSubscription.Price.Should().Be(87);                                  // 29 × 3 = 87$

            _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<TenantSubscription>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: Tenant غير موجود — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewRequestAsync_WhenTenantNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((Tenant?)null);

            var dto = new RenewTenantSubscriptionDto { Plan = TenantPlan.Basic, DurationMonths = 1 };

            // Act
            var act = async () => await _service.RenewRequestAsync(999, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الوكيل غير موجود*");
        }

        // الاختبار الثالث: يوجد طلب معلق مسبقاً — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewRequestAsync_WhenPendingRequestExists_ShouldThrowInvalidOperationException()
        {
            // Arrange
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(CreateFakeTenant());

            // نرجع Subscription معلق موجود مسبقاً
            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TenantSubscription, bool>>>()))
                .ReturnsAsync(new List<TenantSubscription>
                {
                    CreateFakeSubscription(status: TenantSubscriptionStatus.Pending)
                });

            var dto = new RenewTenantSubscriptionDto { Plan = TenantPlan.Basic, DurationMonths = 1 };

            // Act
            var act = async () => await _service.RenewRequestAsync(TenantId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*يوجد طلب تجديد معلق*");
        }

        // الاختبار الرابع: حساب سعر Pro Plan صحيح
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewRequestAsync_WithProPlan6Months_ShouldCalculateCorrectPrice()
        {
            // Arrange
            var dto = new RenewTenantSubscriptionDto
            {
                Plan = TenantPlan.Pro,
                DurationMonths = 6 // المتوقع: 99 × 6 = 594$
            };

            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(CreateFakeTenant());

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TenantSubscription, bool>>>()))
                .ReturnsAsync(new List<TenantSubscription>());

            TenantSubscription? capturedSubscription = null;
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantSubscription>()))
                .ReturnsAsync((TenantSubscription s) => { capturedSubscription = s; return s; });

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mapperMock
                .Setup(m => m.Map<TenantSubscriptionDto>(It.IsAny<TenantSubscription>()))
                .Returns(new TenantSubscriptionDto());

            // Act
            await _service.RenewRequestAsync(TenantId, dto);

            // Assert
            capturedSubscription!.Price.Should().Be(594); // 99 × 6 = 594$
        }

        // ============================================
        // ConfirmPaymentAsync Tests
        // ============================================

        // الاختبار الأول: تأكيد دفع صحيح — يجب أن يُفعِّل الـ Tenant وينشئ TenantPayment
        [Fact]
        [Trait("Category", "Service")]
        public async Task ConfirmPaymentAsync_WithValidData_ShouldActivateTenantAndCreatePayment()
        {
            // Arrange
            var subscription = CreateFakeSubscription(status: TenantSubscriptionStatus.Pending);
            var tenant = CreateFakeTenant();
            tenant.IsActive = false; // الـ Tenant معطَّل ينتظر الدفع

            var dto = new ConfirmTenantPaymentDto
            {
                SubscriptionId = 1,
                PaymentMethod = "Bank Transfer",
                TransactionId = "TXN-12345",
                Notes = "تم الدفع"
            };

            // نرجع الـ Subscription المعلق
            _subscriptionRepoMock.Setup(r => r.GetByIdAsync(dto.SubscriptionId)).ReturnsAsync(subscription);
            _subscriptionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<TenantSubscription>())).Returns(Task.CompletedTask);

            // نرجع الـ Tenant المعطَّل
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(tenant);
            _tenantRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            // نلتقط الـ TenantPayment الذي يُنشأ
            TenantPayment? capturedPayment = null;
            _tenantPaymentRepoMock
                .Setup(r => r.AddAsync(It.IsAny<TenantPayment>()))
                .ReturnsAsync((TenantPayment p) => { capturedPayment = p; return p; });

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.ConfirmPaymentAsync(TenantId, dto);

            // Assert
            tenant.IsActive.Should().BeTrue();                                  // الـ Tenant أصبح مفعَّلاً
            subscription.Status.Should().Be(TenantSubscriptionStatus.Active);   // الاشتراك أصبح Active
            subscription.PaymentMethod.Should().Be("Bank Transfer");            // طريقة الدفع محفوظة

            capturedPayment.Should().NotBeNull();
            capturedPayment!.Status.Should().Be("Completed");          // الدفع مكتمل
            capturedPayment.PaymentMethod.Should().Be("Bank Transfer"); // طريقة الدفع صحيحة
            capturedPayment.TransactionId.Should().Be("TXN-12345");     // رقم العملية محفوظ

            _tenantPaymentRepoMock.Verify(r => r.AddAsync(It.IsAny<TenantPayment>()), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: الاشتراك غير موجود — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task ConfirmPaymentAsync_WhenSubscriptionNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new ConfirmTenantPaymentDto { SubscriptionId = 999, PaymentMethod = "Cash" };

            // نرجع null لمحاكاة عدم وجود الاشتراك
            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((TenantSubscription?)null);

            // Act
            var act = async () => await _service.ConfirmPaymentAsync(TenantId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الاشتراك غير موجود*");
        }

        // الاختبار الثالث: الاشتراك ليس Pending — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task ConfirmPaymentAsync_WhenSubscriptionNotPending_ShouldThrowInvalidOperationException()
        {
            // Arrange
            // الاشتراك Active بالفعل — لا يمكن تأكيده مرة ثانية
            var subscription = CreateFakeSubscription(status: TenantSubscriptionStatus.Active);
            var dto = new ConfirmTenantPaymentDto { SubscriptionId = 1, PaymentMethod = "Cash" };

            _subscriptionRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(subscription);

            // Act
            var act = async () => await _service.ConfirmPaymentAsync(TenantId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*ليس في حالة انتظار*");
        }

        // الاختبار الرابع: الاشتراك لا يخص هذا الـ Tenant — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task ConfirmPaymentAsync_WhenSubscriptionBelongsToDifferentTenant_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var subscription = CreateFakeSubscription(status: TenantSubscriptionStatus.Pending);
            subscription.TenantId = 99; // الاشتراك يخص Tenant آخر وليس TenantId = 1

            var dto = new ConfirmTenantPaymentDto { SubscriptionId = 1, PaymentMethod = "Cash" };

            _subscriptionRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(subscription);

            // Act
            var act = async () => await _service.ConfirmPaymentAsync(TenantId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*لا يخص هذا الوكيل*");
        }

        // الاختبار الخامس: Tenant غير موجود عند تأكيد الدفع — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task ConfirmPaymentAsync_WhenTenantNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var subscription = CreateFakeSubscription(status: TenantSubscriptionStatus.Pending);
            var dto = new ConfirmTenantPaymentDto { SubscriptionId = 1, PaymentMethod = "Cash" };

            _subscriptionRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(subscription);
            _subscriptionRepoMock.Setup(r => r.UpdateAsync(It.IsAny<TenantSubscription>())).Returns(Task.CompletedTask);
            _tenantPaymentRepoMock.Setup(r => r.AddAsync(It.IsAny<TenantPayment>())).ReturnsAsync(new TenantPayment());

            // لا يوجد Tenant — يجب أن يرمي استثناء في نهاية العملية
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync((Tenant?)null);

            // Act
            var act = async () => await _service.ConfirmPaymentAsync(TenantId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الوكيل غير موجود*");
        }

        // ============================================
        // GetPendingRenewalsAsync Tests
        // ============================================

        // الاختبار الأول: يرجع كل الطلبات المعلقة
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetPendingRenewalsAsync_ShouldReturnAllPendingSubscriptions()
        {
            // Arrange
            var pendingSubscriptions = new List<TenantSubscription>
            {
                CreateFakeSubscription(id: 1, status: TenantSubscriptionStatus.Pending),
                CreateFakeSubscription(id: 2, status: TenantSubscriptionStatus.Pending)
            };

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TenantSubscription, bool>>>()))
                .ReturnsAsync(pendingSubscriptions);

            var expectedDtos = new List<TenantSubscriptionDto>
            {
                new TenantSubscriptionDto { Id = 1, Status = "Pending" },
                new TenantSubscriptionDto { Id = 2, Status = "Pending" }
            };

            _mapperMock
                .Setup(m => m.Map<IEnumerable<TenantSubscriptionDto>>(It.IsAny<IEnumerable<TenantSubscription>>()))
                .Returns(expectedDtos);

            // Act
            var result = await _service.GetPendingRenewalsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);                                    // طلبان معلقان
            result.Should().OnlyContain(s => s.Status == "Pending");         // كلهم Pending
        }

        // الاختبار الثاني: لا توجد طلبات معلقة — يرجع قائمة فارغة
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetPendingRenewalsAsync_WhenNoPendingRequests_ShouldReturnEmptyList()
        {
            // Arrange
            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<TenantSubscription, bool>>>()))
                .ReturnsAsync(new List<TenantSubscription>());

            _mapperMock
                .Setup(m => m.Map<IEnumerable<TenantSubscriptionDto>>(It.IsAny<IEnumerable<TenantSubscription>>()))
                .Returns(new List<TenantSubscriptionDto>());

            // Act
            var result = await _service.GetPendingRenewalsAsync();

            // Assert
            result.Should().BeEmpty(); // لا يوجد شيء معلق
        }
    }
}