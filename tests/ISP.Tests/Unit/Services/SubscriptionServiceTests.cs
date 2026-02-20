// ============================================
// SubscriptionServiceTests.cs
// Unit tests for SubscriptionService
// ============================================

using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ISP.Tests.Unit.Services
{
    public class SubscriptionServiceTests
    {
        // ============================================
        // Mocks — fake replacements for the database
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ICurrentTenantService> _currentTenantMock;
        private readonly Mock<ILogger<SubscriptionService>> _loggerMock;
        private readonly Mock<IRepository<Subscription>> _subscriptionRepoMock;
        private readonly Mock<IRepository<Subscriber>> _subscriberRepoMock;
        private readonly Mock<IRepository<Plan>> _planRepoMock;

        private readonly SubscriptionService _service;

        private const int TenantId = 1;

        // ============================================
        // Constructor — runs automatically before each test
        // ============================================

        public SubscriptionServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _currentTenantMock = new Mock<ICurrentTenantService>();
            _loggerMock = new Mock<ILogger<SubscriptionService>>();
            _subscriptionRepoMock = new Mock<IRepository<Subscription>>();
            _subscriberRepoMock = new Mock<IRepository<Subscriber>>();
            _planRepoMock = new Mock<IRepository<Plan>>();

            // ربط كل Repository بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.Subscriptions).Returns(_subscriptionRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Subscribers).Returns(_subscriberRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Plans).Returns(_planRepoMock.Object);

            // كل الاختبارات تعمل تحت TenantId = 1
            _currentTenantMock.Setup(c => c.TenantId).Returns(TenantId);
            _currentTenantMock.Setup(c => c.HasTenant).Returns(true);
            _currentTenantMock.Setup(c => c.IsSuperAdmin).Returns(false);

            // إنشاء الـ Service الحقيقي بالـ Mocks
            _service = new SubscriptionService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _currentTenantMock.Object,
                _loggerMock.Object
            );
        }

        // ============================================
        // Helper Methods — بيانات وهمية جاهزة للاستخدام
        // ============================================

        private Subscriber CreateFakeSubscriber(int id = 1) => new Subscriber
        {
            Id = id,
            TenantId = TenantId,
            FullName = "Ahmed Mohammed",
            PhoneNumber = "0501234567"
        };

        // isActive تتحكم في ما إذا كانت الباقة متاحة للاشتراك
        private Plan CreateFakePlan(int id = 1, bool isActive = true) => new Plan
        {
            Id = id,
            TenantId = TenantId,
            Name = "50 Mbps Plan",
            Speed = 50,
            Price = 100,
            DurationDays = 30,
            IsActive = isActive
        };

        // isDeleted تتحكم في ما إذا كان الاشتراك محذوفاً بـ Soft Delete
        private Subscription CreateFakeSubscription(int id = 1, bool isDeleted = false) => new Subscription
        {
            Id = id,
            TenantId = TenantId,
            SubscriberId = 1,
            PlanId = 1,
            Plan = CreateFakePlan(),
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            Status = SubscriptionStatus.Active,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };

        private SubscriptionDto CreateFakeSubscriptionDto(int id = 1) => new SubscriptionDto
        {
            Id = id,
            SubscriberId = 1,
            PlanId = 1,
            Status = "Active"
        };

        // ============================================
        // CreateAsync Tests
        // ============================================

        // الاختبار الأول: البيانات صحيحة — يجب أن ينجح وينشئ الاشتراك
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithValidData_ShouldReturnSubscriptionDto()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = DateTime.UtcNow
            };

            var subscriber = CreateFakeSubscriber();
            var plan = CreateFakePlan();
            var subscription = CreateFakeSubscription();
            var expectedDto = CreateFakeSubscriptionDto();

            // عندما يطلب المشترك رقم 1، أرجع المشترك الوهمي
            _subscriberRepoMock
                .Setup(r => r.GetByIdAsync(dto.SubscriberId))
                .ReturnsAsync(subscriber);

            // عندما يطلب الباقة رقم 1، أرجع الباقة الوهمية
            _planRepoMock
                .Setup(r => r.GetByIdAsync(dto.PlanId))
                .ReturnsAsync(plan);

            // عندما يحول الـ dto إلى Subscription، أرجع الاشتراك الوهمي
            _mapperMock
                .Setup(m => m.Map<Subscription>(dto))
                .Returns(subscription);

            // It.IsAny: اقبل أي اشتراك بغض النظر عن قيمه
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Subscription>()))
                .ReturnsAsync(subscription);

            // عندما يحول أي Subscription إلى SubscriptionDto، أرجع الـ dto المتوقع
            _mapperMock
                .Setup(m => m.Map<SubscriptionDto>(It.IsAny<Subscription>()))
                .Returns(expectedDto);

            // تظاهر بأن الحفظ نجح وتم حفظ صف واحد
            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedDto.Id);
        }

        // الاختبار الثاني: المشترك غير موجود — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WhenSubscriberNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 99, // رقم وهمي غير موجود في قاعدة البيانات
                PlanId = 1,
                StartDate = DateTime.UtcNow
            };

            // نرجع null لمحاكاة عدم وجود المشترك
            _subscriberRepoMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((Subscriber?)null);

            // Act
            // نضع الاستدعاء في Lambda لأننا نتوقع استثناءً
            // لو استدعيناه مباشرة، الاستثناء سيوقف الاختبار كله
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // الاختبار الثالث: الباقة غير موجودة — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WhenPlanNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 99, // باقة غير موجودة
                StartDate = DateTime.UtcNow
            };

            // المشترك موجود — نريد أن يصل الكود للتحقق من الباقة
            // لو أخفينا المشترك أيضاً لن نعرف أي تحقق هو الذي فشل
            _subscriberRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(CreateFakeSubscriber());

            // الباقة غير موجودة — نكتب (Plan?)null لأن المترجم يحتاج معرفة النوع
            _planRepoMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((Plan?)null);

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // الاختبار الرابع: الباقة موجودة لكنها غير نشطة — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WhenPlanIsInactive_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = DateTime.UtcNow
            };

            _subscriberRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(CreateFakeSubscriber());

            // الفرق الوحيد — الباقة موجودة لكن IsActive = false
            _planRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(CreateFakePlan(isActive: false));

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // الاختبار الخامس: SaveChanges يُستدعى مرة واحدة فقط
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_ShouldSaveChangesExactlyOnce()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = DateTime.UtcNow
            };

            _subscriberRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(CreateFakeSubscriber());
            _planRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(CreateFakePlan());
            _mapperMock.Setup(m => m.Map<Subscription>(dto)).Returns(CreateFakeSubscription());
            _subscriptionRepoMock.Setup(r => r.AddAsync(It.IsAny<Subscription>())).ReturnsAsync(CreateFakeSubscription());
            _mapperMock.Setup(m => m.Map<SubscriptionDto>(It.IsAny<Subscription>())).Returns(CreateFakeSubscriptionDto());
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.CreateAsync(dto);

            // Assert
            // Verify: تتحقق أن SaveChanges استُدعيت مرة واحدة بالضبط
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // ============================================
        // RenewAsync Tests
        // ============================================

        // الاختبار الأول: تجديد ناجح — يلغي القديم وينشئ جديداً
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewAsync_WithSamePlan_ShouldSoftDeleteOldAndCreateNew()
        {
            // Arrange
            var dto = new RenewSubscriptionDto
            {
                SubscriptionId = 1,
                NewPlanId = null // null = نفس الباقة القديمة
            };

            var oldSubscription = CreateFakeSubscription(id: 1);

            // عندما يطلب الاشتراك القديم، أرجعه
            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(oldSubscription);

            // عندما يطلب الباقة، أرجع الباقة الوهمية
            _planRepoMock
                .Setup(r => r.GetByIdAsync(oldSubscription.PlanId))
                .ReturnsAsync(CreateFakePlan());

            // SoftDelete لا يرجع قيمة — نستخدم Task.CompletedTask
            _subscriptionRepoMock
                .Setup(r => r.SoftDeleteAsync(oldSubscription))
                .Returns(Task.CompletedTask);

            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Subscription>()))
                .ReturnsAsync(CreateFakeSubscription(id: 2));

            _mapperMock
                .Setup(m => m.Map<SubscriptionDto>(It.IsAny<Subscription>()))
                .Returns(CreateFakeSubscriptionDto(id: 2));

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act 
            await _service.RenewAsync(dto);

            // Assert
            // Verify: نتأكد أن SoftDelete استُدعي على الاشتراك القديم بالضبط
            _subscriptionRepoMock.Verify(r => r.SoftDeleteAsync(oldSubscription), Times.Once);
            // نتأكد أن اشتراكاً جديداً أُضيف
            _subscriptionRepoMock.Verify(r => r.AddAsync(It.IsAny<Subscription>()), Times.Once);
        }

        // الاختبار الثاني: الاشتراك القديم غير موجود — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewAsync_WhenSubscriptionNotFound_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new RenewSubscriptionDto
            {
                SubscriptionId = 99 // اشتراك غير موجود
            };

            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((Subscription?)null);

            // Act
            var act = async () => await _service.RenewAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        // الاختبار الثالث: الباقة الجديدة غير نشطة — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewAsync_WhenNewPlanIsInactive_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = new RenewSubscriptionDto
            {
                SubscriptionId = 1,
                NewPlanId = 2 // باقة جديدة لكنها غير نشطة
            };

            var oldSubscription = CreateFakeSubscription(id: 1);

            // الاشتراك القديم موجود — لنتأكد أن الكود يصل للتحقق من الباقة
            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(oldSubscription);

            // الباقة الجديدة موجودة لكن IsActive = false
            _planRepoMock
                .Setup(r => r.GetByIdAsync(2))
                .ReturnsAsync(CreateFakePlan(id: 2, isActive: false));

            // Act 
            var act = async () => await _service.RenewAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

        }

        // الاختبار الرابع: التجديد بباقة جديدة — يجب أن يستخدم الباقة الجديدة
        [Fact]
        [Trait("Category", "Service")]
        public async Task RenewAsync_WithNewPlan_ShouldUseNewPlanId()
        {
            // Arrange
            var dto = new RenewSubscriptionDto
            {
                SubscriptionId = 1,
                NewPlanId = 2 // باقة جديدة مختلفة
            };

            var oldSubscription = CreateFakeSubscription(id: 1); // PlanId = 1

            // متغير يلتقط الاشتراك الجديد عند إضافته
            Subscription? capturedNewSubscription = null;

            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(oldSubscription);

            // الباقة الجديدة موجودة ونشطة
            _planRepoMock
                .Setup(r => r.GetByIdAsync(2))
                .ReturnsAsync(CreateFakePlan(id: 2));

            _subscriptionRepoMock
                .Setup(r => r.SoftDeleteAsync(oldSubscription))
                .Returns(Task.CompletedTask);

            // Callback: التقط الاشتراك الجديد عند استدعاء AddAsync
            _subscriptionRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Subscription>()))
                .Callback<Subscription>(s => capturedNewSubscription = s)
                .ReturnsAsync((Subscription s) => s);

            _mapperMock
                .Setup(m => m.Map<SubscriptionDto>(It.IsAny<Subscription>()))
                .Returns(CreateFakeSubscriptionDto());

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.RenewAsync(dto);

            // Assert
            // نتأكد أن الاشتراك الجديد يستخدم الباقة الجديدة PlanId = 2
            capturedNewSubscription.Should().NotBeNull();
            capturedNewSubscription!.PlanId.Should().Be(2);
        }

        // ============================================
        // CancelAsync Tests
        // ============================================

        // الاختبار الأول: إلغاء اشتراك موجود — يرجع true ويستدعي SoftDelete
        [Fact]
        [Trait("Category", "Service")]
        public async Task CancelAsync_WhenSubscriptionExists_ShouldReturnTrueAndSoftDelete()
        {
            // Arrange
            var subscription = CreateFakeSubscription(id: 1);

            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(subscription);

            _subscriptionRepoMock
                .Setup(r => r.SoftDeleteAsync(subscription))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act 
            var result = await _service.CancelAsync(1);

            // Assert 
            // نتأكد أن الدالة رجعت true
            result.Should().BeTrue();

            // Delete وليس  SoftDelete نتأكد أن استُدعي
            _subscriptionRepoMock.Verify(r => r.SoftDeleteAsync(subscription), Times.Once);

            // الحقيقي لم يُستدعَ أبد Delete نتأكد أن 
            _subscriptionRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Subscription>()), Times.Never);
        }

        // الاختبار الثاني: الاشتراك غير موجود — يرجع false ولا يستدعي SoftDelete
        [Fact]
        [Trait("Category", "Service")]
        public async Task CancelAsync_WhenSubscriptionNotFound_ShouldReturnFalse()
        {
            // Arrange
            _subscriptionRepoMock
                .Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((Subscription?)null);

            // Act
            var result = await _service.CancelAsync(99);

            // Assert
            // نتأكد أن الدالة رجعت false
            result.Should().BeFalse();

            // نتأكد أن SoftDelete لم يُستدعَ أبداً — لا يوجد شيء يُحذف
            _subscriptionRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<Subscription>()), Times.Never);
        }

        // ============================================
        // RestoreAsync Tests
        // ============================================

        // الاختبار الأول: استرجاع اشتراك محذوف — يرجع true ويحفظ
        [Fact]
        [Trait("Category", "Service")]
        public async Task RestoreAsync_WhenSubscriptionIsDeleted_ShouldReturnTrue()
        {
            // Arrange 
            _subscriptionRepoMock
                .Setup(r => r.RestoreByIdAsync(1))
                .ReturnsAsync(true);

            _unitOfWorkMock
                .Setup(r => r.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act 
            var result = await _service.RestoreAsync(1);

            // Assert 
            result.Should().BeTrue();

            // استُدعي لأن الاسترجاع نجح SaveChanges نتأكد أن 
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: الاشتراك غير موجود — يرجع false ولا يحفظ
        [Fact]
        [Trait("Category", "Service")]
        public async Task RestoreAsync_WhenSubscriptionNotFound_ShouldReturnFalseAndNotSave()
        {
            // Arrange
            _subscriptionRepoMock
                .Setup(r => r.RestoreByIdAsync(99))
                .ReturnsAsync(false);

            // Act
            var result = await _service.RestoreAsync(99);

            // Assert
            result.Should().BeFalse();

            // لا يوجد شيء تغيّر — SaveChanges لا يجب أن يُستدعى
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        // ============================================
        // PermanentDeleteAsync Tests
        // ============================================

        // الاختبار الأول: حذف نهائي لاشتراك محذوف soft — يرجع true
        [Fact]
        [Trait("Category", "Service")]
        public async Task PermanentDeleteAsync_WhenSubscriptionIsSoftDeleted_ShouldReturnTrue()
        {
            // Arrange
            // isDeleted: true — الاشتراك محذوف soft وجاهز للحذف النهائي
            var deletedSubscription = CreateFakeSubscription(isDeleted: true);

            // IgnoreQueryFilters — يجلب حتى المحذوفات بـ Soft Delete
            _subscriptionRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(1))
                .ReturnsAsync(deletedSubscription);

            _subscriptionRepoMock
                .Setup(r => r.DeleteAsync(deletedSubscription))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            var result = await _service.PermanentDeleteAsync(1);

            // Assert
            result.Should().BeTrue();

            // نتأكد أن Delete الحقيقي استُدعي — وليس SoftDelete
            _subscriptionRepoMock.Verify(r => r.DeleteAsync(deletedSubscription), Times.Once);
        }

        // الاختبار الثاني: الاشتراك نشط — لا يمكن حذفه نهائياً
        [Fact]
        [Trait("Category", "Service")]
        public async Task PermanentDeleteAsync_WhenSubscriptionIsNotSoftDeleted_ShouldThrow()
        {
            // Arrange 
            // isDeleted: false — الاشتراك نشط ولم يُلغَ بعد
            var activeSubscription = CreateFakeSubscription(isDeleted: false);

            _subscriptionRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(1))
                .ReturnsAsync(activeSubscription);

            // Act 
            var act = async () => await _service.PermanentDeleteAsync(1);

            // Assert 
            await act.Should().ThrowAsync<InvalidOperationException>();

            // لم يُستدعَ أبداً Delete نتأكد أن 
            _subscriptionRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Subscription>()), Times.Never);
        }

        // الاختبار الثالث: الاشتراك غير موجود — يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task PermanentDeleteAsync_WhenSubscriptionNotFound_ShouldReturnFalse()
        {
            // Arrange
            _subscriptionRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(99))
                .ReturnsAsync((Subscription?)null);

            // Act
            var result = await _service.PermanentDeleteAsync(99);

            // Assert
            result.Should().BeFalse();

            // لا يوجد شيء يُحذف
            _subscriptionRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Subscription>()), Times.Never);
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
            // ننشئ 15 اشتراكاً وهمياً لاختبار الـ Pagination
            var subscriptions = Enumerable.Range(1, 15)
                .Select(i => CreateFakeSubscription(id: i))
                .ToList();

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(subscriptions);

            _mapperMock
                .Setup(m => m.Map<List<SubscriptionDto>>(It.IsAny<object>()))
                .Returns(new List<SubscriptionDto>());

            // Act
            var result = await _service.GetAllAsync(pageNumber: 1, pageSize: 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(15);   // المجموع الكلي 15
            result.PageNumber.Should().Be(1);     // الصفحة الأولى
            result.PageSize.Should().Be(10);      // 10 عناصر في الصفحة
            result.TotalPages.Should().Be(2);     // 15 / 10 = صفحتان
            result.HasPrevious.Should().BeFalse();// الصفحة الأولى لا تملك سابقة
            result.HasNext.Should().BeTrue();     // يوجد صفحة ثانية
        }

        // الاختبار الثاني: الصفحة الثانية تحتوي على البيانات الصحيحة
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetAllAsync_WithPage2_ShouldReturnCorrectPaginationFlags()
        {
            // Arrange
            var subscriptions = Enumerable.Range(1, 15)
                .Select(i => CreateFakeSubscription(id: i))
                .ToList();

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(subscriptions);

            _mapperMock
                .Setup(m => m.Map<List<SubscriptionDto>>(It.IsAny<object>()))
                .Returns(new List<SubscriptionDto>());

            // Act
            var result = await _service.GetAllAsync(pageNumber: 2, pageSize: 10);

            // Assert
            result.PageNumber.Should().Be(2);    // نحن في الصفحة الثانية
            result.HasPrevious.Should().BeTrue(); // يوجد صفحة أولى قبلها
            result.HasNext.Should().BeFalse();    // لا يوجد صفحة ثالثة
        }

        // ============================================
        // UpdateStatusesAsync Tests
        // ============================================

        // الاختبار الأول: الحالة تغيرت — يجب أن يستدعي Update
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateStatusesAsync_WhenStatusChanges_ShouldCallUpdate()
        {
            // Arrange
            // اشتراك منتهي لكن حالته Active — خطأ يجب تصحيحه
            var expiredSubscription = new Subscription
            {
                Id = 1,
                TenantId = TenantId,
                Plan = CreateFakePlan(),
                EndDate = DateTime.UtcNow.AddDays(-5), // منتهي منذ 5 أيام
                Status = SubscriptionStatus.Active    // حالة قديمة خاطئة
            };

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Subscription> { expiredSubscription });

            _subscriptionRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Subscription>()))
                .Returns(Task.CompletedTask);

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.UpdateStatusesAsync();

            // Assert
            // الحالة تغيرت من Active إلى Expired — Update يجب أن يُستدعى
            _subscriptionRepoMock.Verify(r => r.UpdateAsync(expiredSubscription), Times.Once);
        }

        // الاختبار الثاني: الحالة لم تتغير — لا يجب أن يستدعي Update
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateStatusesAsync_WhenStatusNotChanged_ShouldNotCallUpdate()
        {
            // Arrange
            // اشتراك نشط وحالته Active — لا يحتاج تحديثاً
            var activeSubscription = new Subscription
            {
                Id = 1,
                TenantId = TenantId,
                Plan = CreateFakePlan(),
                EndDate = DateTime.UtcNow.AddDays(30), // نشط لمدة 30 يوم
                Status = SubscriptionStatus.Active    // حالة صحيحة بالفعل
            };

            _subscriptionRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<Subscription> { activeSubscription });

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // Act
            await _service.UpdateStatusesAsync();

            // Assert
            // الحالة لم تتغير — Update لا يجب أن يُستدعى لتوفير موارد قاعدة البيانات
            _subscriptionRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Subscription>()), Times.Never);
        }
    }
}