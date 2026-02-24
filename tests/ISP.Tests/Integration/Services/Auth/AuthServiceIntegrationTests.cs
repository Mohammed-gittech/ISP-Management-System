// ============================================
// AuthServiceIntegrationTests.cs
// Integration Tests for AuthService
// ============================================
// نختبر هنا تفاعل AuthService مع قاعدة البيانات
// الحقيقية — IPasswordHasher و IJwtTokenService
// يبقيان كـ Mocks لأنهما خارج نطاق DB
// ============================================

using FluentAssertions;
using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Repositories;
using ISP.Infrastructure.Services;
using ISP.Tests.Helpers;
using Moq;

namespace ISP.Tests.Integration.Services
{
    public class AuthServiceIntegrationTests
    {
        // ============================================
        // Mocks — فقط للخدمات الخارجية
        // ============================================

        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;

        public AuthServiceIntegrationTests()
        {
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        }

        // ============================================
        // Helper Methods — بيانات وهمية للاختبارات
        // ============================================

        // ينشئ Tenant وهمي مباشرة في قاعدة البيانات
        private async Task<Tenant> CreateTenantAsync(
            ApplicationDbContext context,
            int id,
            string name,
            bool isActive = true)
        {
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        }

        // ينشئ User وهمي مباشرة في قاعدة البيانات
        private async Task<User> CreateUserAsync(
            ApplicationDbContext context,
            int? tenantId,
            string email,
            string passwordHash = "hashed_password",
            UserRole role = UserRole.Employee,
            bool isActive = true,
            bool isDeleted = false)
        {
            var user = new User
            {
                TenantId = tenantId,
                Username = email.Split('@')[0],
                Email = email,
                PasswordHash = passwordHash,
                Role = role,
                IsActive = isActive,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        // ينشئ AuthService حقيقي مع UnitOfWork حقيقي
        private AuthService CreateService(ApplicationDbContext context, ICurrentTenantService fakeTenant)
        {
            var unitOfWork = new UnitOfWork(context, fakeTenant);

            return new AuthService(
                unitOfWork,
                _passwordHasherMock.Object,
                _jwtTokenServiceMock.Object
            );
        }

        // ============================================
        // LoginAsync Integration Tests
        // ============================================

        // TEST 1: مستخدم موجود في DB — Login يكمل بنجاح
        [Fact]
        public async Task LoginAsync_WithExistingUserInDb_ShouldReturnLoginResponse()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            var user = await CreateUserAsync(context, tenantId: 1, email: "ahmed@alnoor.com");

            var request = new LoginRequestDto
            {
                Email = "ahmed@alnoor.com",
                Password = "Admin@123"
            };

            // كلمة المرور صحيحة
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(It.IsAny<User>()))
                .Returns("fake_jwt_token");

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            result.Should().NotBeNull();
            result!.Email.Should().Be("ahmed@alnoor.com");
            result.Token.Should().Be("fake_jwt_token");
            result.TenantId.Should().Be(1);
        }

        // TEST 2: مستخدم محذوف Soft Delete — لا يستطيع الدخول
        [Fact]
        public async Task LoginAsync_WithSoftDeletedUser_ShouldReturnNull()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف مستخدم محذوف — الـ Global Filter يخفيه
            await CreateUserAsync(context, tenantId: 1, email: "ahmed@alnoor.com", isDeleted: true);

            var request = new LoginRequestDto
            {
                Email = "ahmed@alnoor.com",
                Password = "Admin@123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            // المستخدم المحذوف لا يظهر في GetAllAsync — يرجع null
            result.Should().BeNull();

            _passwordHasherMock.Verify(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // TEST 3: مستخدم غير موجود في DB — يرجع null
        [Fact]
        public async Task LoginAsync_WithNonExistentUser_ShouldReturnNull()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            // قاعدة البيانات فارغة — لا يوجد أي مستخدم
            var request = new LoginRequestDto
            {
                Email = "unknown@alnoor.com",
                Password = "Admin@123"
            };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            result.Should().BeNull();
        }

        // TEST 4: Tenant معطّل في DB — يرمي UnauthorizedAccessException
        [Fact]
        public async Task LoginAsync_WithInactiveTenantInDb_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            // Tenant معطّل في DB حقيقية
            await CreateTenantAsync(context, id: 1, name: "شركة النور", isActive: false);
            var user = await CreateUserAsync(context, tenantId: 1, email: "ahmed@alnoor.com");

            var request = new LoginRequestDto
            {
                Email = "ahmed@alnoor.com",
                Password = "Admin@123"
            };

            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            // Act
            var act = async () => await service.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*حساب الوكيل معطّل*");
        }

        // TEST 5: مستخدم معطّل في DB — يرمي UnauthorizedAccessException
        [Fact]
        public async Task LoginAsync_WithInactiveUserInDb_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // مستخدم معطّل في DB حقيقية
            var user = await CreateUserAsync(
                context,
                tenantId: 1,
                email: "ahmed@alnoor.com",
                isActive: false);

            var request = new LoginRequestDto
            {
                Email = "ahmed@alnoor.com",
                Password = "Admin@123"
            };

            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            // Act
            var act = async () => await service.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*الحساب معطّل*");
        }

        // ============================================
        // ValidateTokenAsync Integration Tests
        // ============================================

        // TEST 6: Token صحيح ومستخدم موجود ونشط في DB — يرجع true
        [Fact]
        public async Task ValidateTokenAsync_WithActiveUserInDb_ShouldReturnTrue()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            var user = await CreateUserAsync(context, tenantId: 1, email: "ahmed@alnoor.com");

            // Token صحيح يرجع UserId
            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken("valid_token"))
                .Returns(user.Id);

            // Act
            var result = await service.ValidateTokenAsync("valid_token");

            // Assert
            result.Should().BeTrue();
        }

        // TEST 7: Token صحيح لكن مستخدم محذوف في DB — يرجع false
        [Fact]
        public async Task ValidateTokenAsync_WithDeletedUserInDb_ShouldReturnFalse()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeTenant = new FakeTenantService(isSuperAdmin: true);
            var service = CreateService(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // مستخدم محذوف — GetByIdAsync لا يجده بسبب Global Filter
            var user = await CreateUserAsync(
                context,
                tenantId: 1,
                email: "ahmed@alnoor.com",
                isDeleted: true);

            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken("valid_token"))
                .Returns(user.Id);

            // Act
            var result = await service.ValidateTokenAsync("valid_token");

            // Assert
            // المستخدم المحذوف لا يظهر — يرجع false
            result.Should().BeFalse();
        }
    }
}