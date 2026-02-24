// ============================================
// AuthServiceTests.cs
// Unit Tests for AuthService
// ============================================

using System.Linq.Expressions;
using FluentAssertions;
using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Moq;

namespace ISP.Tests.Unit.Services
{
    public class AuthServiceTests
    {
        // ============================================
        // Mocks — نسخ وهمية بدل قاعدة البيانات الحقيقية
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IRepository<Tenant>> _tenantRepoMock;

        // SUT — الكود الحقيقي الذي نختبره
        private readonly AuthService _service;

        // ثوابت لتجنب تكرار الأرقام في كل اختبار
        private const int UserId = 1;
        private const int TenantId = 10;

        // ============================================
        // Constructor — يُنفَّذ تلقائياً قبل كل اختبار
        // ============================================

        public AuthServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _jwtTokenServiceMock = new Mock<IJwtTokenService>();
            _userRepoMock = new Mock<IRepository<User>>();
            _tenantRepoMock = new Mock<IRepository<Tenant>>();

            // ربط كل Repository بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);

            // إنشاء الـ Service الحقيقي بالـ Mocks
            _service = new AuthService(
                _unitOfWorkMock.Object,
                _passwordHasherMock.Object,
                _jwtTokenServiceMock.Object
            );
        }

        // ============================================
        // Helper Methods — بيانات وهمية جاهزة
        // ============================================

        // ينشئ User وهمي — role تتحكم في الدور
        private User CreateFakeUser(
            UserRole role = UserRole.Employee,
            bool isActive = true) => new User
            {
                Id = UserId,
                TenantId = role == UserRole.SuperAdmin ? null : TenantId,
                Username = "ahmed_admin",
                Email = "ahmed@alnoor.com",
                PasswordHash = "hashed_password",
                Role = role,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };

        // ينشئ Tenant وهمي — isActive تتحكم في حالة الوكيل
        private Tenant CreateFakeTenant(bool isActive = true) => new Tenant
        {
            Id = TenantId,
            Name = "شركة النور",
            IsActive = isActive
        };

        // ينشئ LoginRequestDto وهمي
        private LoginRequestDto CreateFakeRequest() => new LoginRequestDto
        {
            Email = "ahmed@alnoor.com",
            Password = "Admin@123"
        };

        // ============================================
        // LoginAsync Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة — يجب أن يرجع LoginResponseDto مع Token
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnLoginResponse()
        {
            // Arrange
            var request = CreateFakeRequest();
            var user = CreateFakeUser();
            var tenant = CreateFakeTenant();

            // المستخدم موجود بهذا الـ Email
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            // كلمة المرور صحيحة
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            // Tenant نشط
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(tenant);

            // توليد Token
            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(user))
                .Returns("fake_jwt_token");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            result.Should().NotBeNull();
            result!.Token.Should().Be("fake_jwt_token");
            result.UserId.Should().Be(UserId);
            result.Username.Should().Be("ahmed_admin");
            result.TenantId.Should().Be(TenantId);

            _jwtTokenServiceMock.Verify(j => j.GenerateToken(user), Times.Once);
        }

        // الاختبار الثاني: Email غير موجود — يجب أن يرجع null
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithUnknownEmail_ShouldReturnNull()
        {
            // Arrange
            var request = CreateFakeRequest();

            // لا يوجد مستخدم بهذا الـ Email
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            result.Should().BeNull();

            // لا يجب أن يُستدعى VerifyPassword أو GenerateToken
            _passwordHasherMock.Verify(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _jwtTokenServiceMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
        }

        // الاختبار الثالث: كلمة مرور خاطئة — يجب أن يرجع null
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithWrongPassword_ShouldReturnNull()
        {
            // Arrange
            var request = CreateFakeRequest();
            var user = CreateFakeUser();

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            // كلمة المرور خاطئة
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(false);

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            result.Should().BeNull();

            // لا يجب أن يُستدعى GenerateToken
            _jwtTokenServiceMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
        }

        // الاختبار الرابع: حساب المستخدم معطّل — يجب أن يرمي UnauthorizedAccessException
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithInactiveUser_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var request = CreateFakeRequest();
            var inactiveUser = CreateFakeUser(isActive: false);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { inactiveUser });

            // كلمة المرور صحيحة لكن الحساب معطّل
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, inactiveUser.PasswordHash))
                .Returns(true);

            // Act
            var act = async () => await _service.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*الحساب معطّل*");

            _jwtTokenServiceMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
        }

        // الاختبار الخامس: Tenant معطّل — يجب أن يرمي UnauthorizedAccessException
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithInactiveTenant_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var request = CreateFakeRequest();
            var user = CreateFakeUser();
            var inactiveTenant = CreateFakeTenant(isActive: false);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            // Tenant موجود لكنه معطّل
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(inactiveTenant);

            // Act
            var act = async () => await _service.LoginAsync(request);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*حساب الوكيل معطّل*");

            _jwtTokenServiceMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
        }

        // الاختبار السادس: SuperAdmin بدون Tenant — يجب أن يرجع Token بدون TenantId
        [Fact]
        [Trait("Category", "Service")]
        public async Task LoginAsync_WithSuperAdmin_ShouldReturnTokenWithoutTenantId()
        {
            // Arrange
            var request = CreateFakeRequest();
            var superAdmin = CreateFakeUser(role: UserRole.SuperAdmin);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { superAdmin });

            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, superAdmin.PasswordHash))
                .Returns(true);

            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(superAdmin))
                .Returns("superadmin_jwt_token");

            // Act
            var result = await _service.LoginAsync(request);

            // Assert
            result.Should().NotBeNull();
            result!.TenantId.Should().BeNull();      // SuperAdmin لا يملك TenantId
            result.Role.Should().Be("SuperAdmin");
            result.Token.Should().Be("superadmin_jwt_token");

            // لا يجب أن يستدعي Tenants لأن SuperAdmin لا يملك Tenant
            _tenantRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ============================================
        // ValidateTokenAsync Tests
        // ============================================

        // الاختبار الأول: Token صحيح ومستخدم نشط — يجب أن يرجع true
        [Fact]
        [Trait("Category", "Service")]
        public async Task ValidateTokenAsync_WithValidTokenAndActiveUser_ShouldReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();
            var token = "valid_jwt_token";

            // Token صحيح — يرجع UserId
            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken(token))
                .Returns(UserId);

            // المستخدم موجود ونشط
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(user);

            // Act
            var result = await _service.ValidateTokenAsync(token);

            // Assert
            result.Should().BeTrue();
        }

        // الاختبار الثاني: Token غير صالح — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ValidateTokenAsync_WithInvalidToken_ShouldReturnFalse()
        {
            // Arrange
            var token = "invalid_jwt_token";

            // Token غير صالح — يرجع null
            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken(token))
                .Returns((int?)null);

            // Act
            var result = await _service.ValidateTokenAsync(token);

            // Assert
            result.Should().BeFalse();

            // لا يجب أن يستدعي Users لأن الـ Token فشل أولاً
            _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // الاختبار الثالث: Token صحيح لكن مستخدم غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ValidateTokenAsync_WithValidTokenButUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            var token = "valid_jwt_token";

            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken(token))
                .Returns(UserId);

            // المستخدم غير موجود في قاعدة البيانات
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.ValidateTokenAsync(token);

            // Assert
            result.Should().BeFalse();
        }

        // الاختبار الرابع: Token صحيح لكن مستخدم معطّل — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ValidateTokenAsync_WithValidTokenButInactiveUser_ShouldReturnFalse()
        {
            // Arrange
            var token = "valid_jwt_token";
            var inactiveUser = CreateFakeUser(isActive: false);

            _jwtTokenServiceMock
                .Setup(j => j.ValidateToken(token))
                .Returns(UserId);

            // المستخدم موجود لكنه معطّل
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(inactiveUser);

            // Act
            var result = await _service.ValidateTokenAsync(token);

            // Assert
            result.Should().BeFalse();
        }
    }
}