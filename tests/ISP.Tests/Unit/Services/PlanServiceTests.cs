using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs.Plans;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ISP.Tests.Unit.Services
{
    public class PlanServiceTests
    {
        // Dependencies - المعتمديات المزيفة
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentTenantService> _mockCurrentTenant;
        private readonly Mock<ILogger<PlanService>> _mockLogger;
        private readonly Mock<IRepository<Plan>> _mockPlanRepo;

        private readonly Mock<IRepository<Subscription>> _mockSubscriptionRepo;

        // SUT (System Under Test) - الكود المُختبر
        private readonly PlanService _service;

        public PlanServiceTests()
        {
            // 1. إنشاء Mocks
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentTenant = new Mock<ICurrentTenantService>();
            _mockLogger = new Mock<ILogger<PlanService>>();
            _mockPlanRepo = new Mock<IRepository<Plan>>();
            _mockSubscriptionRepo = new Mock<IRepository<Subscription>>();

            // 2. Setup UnitOfWork ليُرجع Repositories
            _mockUnitOfWork.Setup(u => u.Plans).Returns(_mockPlanRepo.Object);
            _mockUnitOfWork.Setup(u => u.Subscriptions).Returns(_mockSubscriptionRepo.Object);

            // 3. Setup CurrentTenant (افتراضياً TenantId = 1)
            _mockCurrentTenant.Setup(t => t.TenantId).Returns(1);

            // 4. إنشاء Service مع Mocks
            _service = new PlanService(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentTenant.Object,
                _mockLogger.Object
            );
        }

        // TEST 1: CreateAsync - Valid
        [Fact]
        public async Task CreateAsync_ValidDto_ReturnsPlanDto()
        {
            // Arrange
            var createDto = new CreatePlanDto
            {
                Name = "باقة 50 ميجا",
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
                Description = "باقة اقتصادية للاستخدام المنزلي",
            };

            _mockMapper.Setup(m => m.Map<Plan>(It.IsAny<CreatePlanDto>())).Returns(new Plan());

            _mockPlanRepo
                .Setup(p => p.AddAsync(It.IsAny<Plan>()))
                .ReturnsAsync(
                    (Plan p) =>
                    {
                        p.Id = 1;
                        p.TenantId = 1;
                        return p;
                    }
                );

            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            var responseDto = new PlanDto
            {
                Id = 1,
                Name = "باقة 50 ميجا",
                Speed = 50,
                Price = 15000,
                IsActive = true,
            };

            _mockMapper.Setup(m => m.Map<PlanDto>(It.IsAny<Plan>())).Returns(responseDto);

            // Act
            var result = await _service.CreateAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Name.Should().Be("باقة 50 ميجا");
            result.Speed.Should().Be(50);
            result.Price.Should().Be(15000);

            _mockPlanRepo.Verify(p => p.AddAsync(It.IsAny<Plan>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // TEST 2: GetByIdAsync - Existing
        [Fact]
        public async Task GetByIdAsync_ExistingId_ReturnsPlanDto()
        {
            // Arrange
            var plan = new Plan
            {
                Id = 5,
                TenantId = 1,
                Name = "باقة 100 ميجا",
                Speed = 100,
                Price = 25000,
                IsActive = true,
            };

            _mockPlanRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(plan);

            var responseDto = new PlanDto
            {
                Id = 5,
                Name = "باقة 100 ميجا",
                Speed = 100,
                Price = 25000,
            };

            _mockMapper.Setup(x => x.Map<PlanDto>(plan)).Returns(responseDto);

            // Act
            var result = await _service.GetByIdAsync(5);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(5);
            result.Name.Should().Be("باقة 100 ميجا");
            result.Speed.Should().Be(100);
        }

        // TEST 3: GetByIdAsync - Non-Existing
        [Fact]
        public async Task GetByIdAsync_NotExistingID_ReturnNull()
        {
            // Arrange
            _mockPlanRepo.Setup(p => p.GetByIdAsync(999)).ReturnsAsync((Plan?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
        }

        // TEST 4: GetActiveAsync
        [Fact]
        public async Task GetActiveAsync_ReturnsOnlyActivePlan()
        {
            // Arrange
            var activePlans = new List<Plan>
            {
                new Plan
                {
                    Id = 1,
                    Name = "باقة 50",
                    Speed = 50,
                    IsActive = true,
                },
                new Plan
                {
                    Id = 2,
                    Name = "باقة 100",
                    Speed = 100,
                    IsActive = true,
                },
            };

            _mockPlanRepo
                .Setup(p => p.GetAllAsync(It.IsAny<Expression<Func<Plan, bool>>>()))
                .ReturnsAsync(activePlans);

            var responseDto = new List<PlanDto>
            {
                new PlanDto
                {
                    Id = 1,
                    Name = "باقة 50",
                    Speed = 50,
                },
                new PlanDto
                {
                    Id = 2,
                    Name = "باقة 100",
                    Speed = 100,
                },
            };

            _mockMapper
                .Setup(m => m.Map<List<PlanDto>>(It.IsAny<List<Plan>>()))
                .Returns(responseDto);

            // Act
            var result = await _service.GetActiveAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(p => p.Id == 1 || p.Id == 2);
        }

        // TEST 5: UpdateAsync - Valid
        [Fact]
        public async Task UpdateAsync_ValidDto_UpdatesSuccessfuly()
        {
            // Arrange
            var existingPlan = new Plan
            {
                Id = 3,
                TenantId = 1,
                Speed = 50,
                Price = 15000,
            };

            _mockPlanRepo.Setup(p => p.GetByIdAsync(3)).ReturnsAsync(existingPlan);

            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            var updateDto = new UpdatePlanDto { Name = "باقة 50 ميجا محدثة", Price = 18000 };

            // Act
            await _service.UpdateAsync(3, updateDto);

            // Assert
            existingPlan.Name.Should().Be("باقة 50 ميجا محدثة");
            existingPlan.Price.Should().Be(18000);

            _mockPlanRepo.Verify(p => p.UpdateAsync(existingPlan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // TEST 6: UpdateAsync - Non-Existing
        [Fact]
        public async Task UpdateAsync_NonExistingPlan_ThrowsException()
        {
            // Arrange
            _mockPlanRepo.Setup(p => p.GetByIdAsync(999)).ReturnsAsync((Plan?)null);

            var updateDto = new UpdatePlanDto { Name = "باقة محدثة" };

            // Act & Assert
            var act = async () => await _service.UpdateAsync(999, updateDto);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*غير موجودة*");
        }

        // TEST 7: DeleteAsync - Valid
        [Fact]
        public async Task DeleteAsync_ExistingPlan_SoftDeletes()
        {
            // Arrange
            var plan = new Plan
            {
                Id = 4,
                TenantId = 1,
                Name = "باقة 25 ميجا",
                IsActive = true,
            };

            _mockPlanRepo.Setup(p => p.GetByIdAsync(4)).ReturnsAsync(plan);

            _mockSubscriptionRepo
                .Setup(s => s.GetAllAsync(It.IsAny<Expression<Func<Subscription, bool>>>()))
                .ReturnsAsync(new List<Subscription>());

            _mockUnitOfWork.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.DeleteAsync(4);

            // Assert
            _mockPlanRepo.Verify(p => p.SoftDeleteAsync(plan), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // TEST 8: Theory - Different Speeds
        [Theory]
        [InlineData(10, "باقة 10 ميجا")]
        [InlineData(50, "باقة 50 ميجا")]
        [InlineData(100, "باقة 100 ميجا")]
        [InlineData(500, "باقة 500 ميجا")]
        public async Task CreateAsync_DifferentSpeeds_CreatesSuccessfuly(int speed, string name)
        {
            // Arrange
            var createDto = new CreatePlanDto
            {
                Name = name,
                Speed = speed,
                Price = speed * 200,
            };

            _mockMapper.Setup(m => m.Map<Plan>(It.IsAny<CreatePlanDto>())).Returns(new Plan());

            _mockPlanRepo
                .Setup(p => p.AddAsync(It.IsAny<Plan>()))
                .ReturnsAsync(
                    (Plan p) =>
                    {
                        p.Id = 1;
                        return p;
                    }
                );

            _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _mockMapper
                .Setup(m => m.Map<PlanDto>(It.IsAny<Plan>()))
                .Returns(
                    new PlanDto
                    {
                        Id = 1,
                        Name = name,
                        Speed = speed,
                    }
                );

            // Act
            var result = await _service.CreateAsync(createDto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be(name);
            result.Speed.Should().Be(speed);
        }
    }
}
