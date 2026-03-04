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
        private readonly Mock<IRepository<RefreshToken>> _refreshTokenRepoMock;

        // SUT = System Under Test
        private readonly AuthService _sut;

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
            _refreshTokenRepoMock = new Mock<IRepository<RefreshToken>>();

            // ربط كل Repository بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.RefreshTokens).Returns(_refreshTokenRepoMock.Object);

            // SaveChangesAsync تُرجع 1 (يعني: تم حفظ صف واحد)
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            _refreshTokenRepoMock
                .Setup(r => r.AddAsync(It.IsAny<RefreshToken>()))
                .ReturnsAsync((RefreshToken rt) => rt);

            // إنشاء الـ Service الحقيقي بالـ Mocks
            _sut = new AuthService(
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

        private RefreshToken CreateFakeRefreshToken(
            bool isRevoked = false,
            DateTime? expiresAt = null) => new RefreshToken
            {
                Id = 1,
                Token = "fake_refresh_token_string",
                UserId = UserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
                IsRevoked = isRevoked,
                RevokedAt = isRevoked ? DateTime.UtcNow : null
            };

        // ============================================
        // LoginAsync Tests
        // ============================================

        // Test 1: Valid credentials
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithValidCredentials_ShouldReturnResponseWithBothTokens()
        {
            // Arrange
            var request = CreateFakeRequest();
            var user = CreateFakeUser();
            var tenant = CreateFakeTenant();

            // When searching by email → return the user
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            // Password is correct
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(request.Password, user.PasswordHash))
                .Returns(true);

            // Tenant exists and is active
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(tenant);

            // JWT generation returns a fake token
            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(user))
                .Returns("fake_jwt_token");

            // Act
            var result = await _sut.LoginAsync(request);

            // Assert

            // Response is not null
            result.Should().NotBeNull();
            // Access Token is correct
            result!.Token.Should().Be("fake_jwt_token");
            // User data is mapped correctly
            result.UserId.Should().Be(UserId);
            result.Username.Should().Be("ahmed_admin");
            result.Email.Should().Be("ahmed@alnoor.com");
            result.TenantId.Should().Be(TenantId);

            // Refresh Token exists and is not empty
            result.RefreshToken.Should().NotBeNullOrEmpty();

            // Access Token expiry is in the future
            result.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

            // Refresh Token was saved to DB
            _refreshTokenRepoMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>()), Times.Once);

            // SaveChangesAsync was called to persist changes
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // Test 2: Unknown email
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithUnknownEmail_ShouldReturnNull()
        {
            // ======== Arrange ========

            // Search returns empty list → user not found
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            // ======== Act ========
            var result = await _sut.LoginAsync(CreateFakeRequest());

            // ======== Assert ========

            // Result must be null
            result.Should().BeNull();

            // VerifyPassword must never be called
            _passwordHasherMock.Verify(
                p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);

            // GenerateToken must never be called
            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            // No Refresh Token should be saved
            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Never);
        }

        // Test 3: Wrong password
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithWrongPassword_ShouldReturnNull()
        {
            // ======== Arrange ========

            var user = CreateFakeUser();

            // User is found
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            // But password is wrong → VerifyPassword returns false
            _passwordHasherMock
                .Setup(p => p.VerifyPassword("Admin@123", user.PasswordHash))
                .Returns(false);

            // ======== Act ========

            var result = await _sut.LoginAsync(CreateFakeRequest());

            // ======== Assert ========

            result.Should().BeNull();

            // GenerateToken must never be called
            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            // No Refresh Token should be saved
            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Never);
        }

        // Test 4: Inactive user account
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithInactiveUser_ShouldThrowUnauthorizedAccessException()
        {
            // ======== Arrange ========

            // isActive: false → disabled account
            var inactiveUser = CreateFakeUser(isActive: false);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { inactiveUser });

            // Password is correct but account is disabled
            _passwordHasherMock
                .Setup(p => p.VerifyPassword("Admin@123", inactiveUser.PasswordHash))
                .Returns(true);

            // ======== Act ========
            // Wrap in lambda so FluentAssertions can catch the exception
            var act = async () => await _sut.LoginAsync(CreateFakeRequest());

            // ======== Assert ========

            // Must throw UnauthorizedAccessException
            await act.Should()
                .ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*الحساب معطّل*");

            // No token should be generated for disabled accounts
            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            // No Refresh Token should be saved
            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Never);
        }

        // Test 5: Inactive tenant
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithInactiveTenant_ShouldThrowUnauthorizedAccessException()
        {
            // ======== Arrange ========

            var user = CreateFakeUser();
            var inactiveTenant = CreateFakeTenant(isActive: false);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { user });

            _passwordHasherMock
                .Setup(p => p.VerifyPassword("Admin@123", user.PasswordHash))
                .Returns(true);

            // Tenant exists but is disabled
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(inactiveTenant);

            // ======== Act ========

            var act = async () => await _sut.LoginAsync(CreateFakeRequest());

            // ======== Assert ========

            await act.Should()
                .ThrowAsync<UnauthorizedAccessException>()
                .WithMessage("*حساب الوكيل معطّل*");

            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Never);
        }

        // Test 6: SuperAdmin login
        [Fact]
        [Trait("Category", "LoginAsync")]
        public async Task LoginAsync_WithSuperAdmin_ShouldReturnResponseWithoutTenantId()
        {
            // ======== Arrange ========

            // role: SuperAdmin → TenantId will be null inside CreateFakeUser
            var superAdmin = CreateFakeUser(role: UserRole.SuperAdmin);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { superAdmin });

            _passwordHasherMock
                .Setup(p => p.VerifyPassword("Admin@123", superAdmin.PasswordHash))
                .Returns(true);

            // No tenant setup needed → any call to Tenants would fail
            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(superAdmin))
                .Returns("superadmin_access_token");

            // ======== Act ========

            var result = await _sut.LoginAsync(CreateFakeRequest());

            // ======== Assert ========

            result.Should().NotBeNull();

            // TenantId must be null for SuperAdmin
            result!.TenantId.Should().BeNull();

            // Role must be SuperAdmin string
            result.Role.Should().Be("SuperAdmin");

            // Access Token is correct
            result.Token.Should().Be("superadmin_access_token");

            // SuperAdmin also gets a Refresh Token
            result.RefreshToken.Should().NotBeNullOrEmpty();

            // Tenants Repository must never be called for SuperAdmin
            _tenantRepoMock.Verify(
                r => r.GetByIdAsync(It.IsAny<int>()),
                Times.Never);

            // Refresh Token was saved
            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Once);

        }

        // ============================================
        // RefreshAccessTokenAsync Tests
        // ============================================

        // Test 1: Valid refresh token
        [Fact]
        [Trait("Category", "RefreshAccessTokenAsync")]
        public async Task RefreshAccessTokenAsync_WithValidToken_ShouldReturnNewTokensAndRotate()
        {
            // ======== Arrange ========

            var user = CreateFakeUser();
            var existingToken = CreateFakeRefreshToken();

            // When searching for the token → return it
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { existingToken });

            // When fetching the user → return active user
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(user);

            // JWT generation returns a new fake token
            _jwtTokenServiceMock
                .Setup(j => j.GenerateToken(user))
                .Returns("new_fake_access_token");

            // ======== Act ========

            var result = await _sut.RefreshAccessTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // Response is not null
            result.Should().NotBeNull();

            // New Access Token is returned
            result!.Token.Should().Be("new_fake_access_token");

            // New Refresh Token exists
            result.RefreshToken.Should().NotBeNullOrEmpty();

            // New Refresh Token is different from the old one (Token Rotation)
            result.RefreshToken.Should().NotBe("fake_refresh_token_string");

            // Access Token expiry is in the future
            result.AccessTokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

            // Old token was revoked (IsRevoked = true + RevokedAt != null)
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.Is<RefreshToken>(t =>
                    t.IsRevoked == true &&
                    t.RevokedAt != null)),
                Times.Once);

            // New token was added to DB
            _refreshTokenRepoMock.Verify(
                r => r.AddAsync(It.IsAny<RefreshToken>()),
                Times.Once);

            // SaveChangesAsync called once (saves both: revoke old + add new)
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Once);
        }

        // Test 2: Non-existent refresh token
        [Fact]
        [Trait("Category", "RefreshAccessTokenAsync")]
        public async Task RefreshAccessTokenAsync_WithNonExistentToken_ShouldReturnNull()
        {
            // ======== Arrange ========

            // Search returns empty list → token not found in DB
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken>());

            // ======== Act ========

            var result = await _sut.RefreshAccessTokenAsync("non_existent_token");

            // ======== Assert ========

            result.Should().BeNull();

            // User must never be fetched if token not found
            _userRepoMock.Verify(
                r => r.GetByIdAsync(It.IsAny<int>()),
                Times.Never);

            // No new token should be generated
            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            // Nothing should be saved to DB
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Never);
        }

        // Test 3: Revoked refresh token
        [Fact]
        [Trait("Category", "RefreshAccessTokenAsync")]
        public async Task RefreshAccessTokenAsync_WithRevokedToken_ShouldReturnNull()
        {
            // ======== Arrange ========

            // isRevoked: true → IsActive = false
            // IsActive = !IsRevoked && !IsExpired = !true && ... = false
            var revokedToken = CreateFakeRefreshToken(isRevoked: true);

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { revokedToken });

            // ======== Act ========

            var result = await _sut.RefreshAccessTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // IsActive = false because IsRevoked = true → return null
            result.Should().BeNull();

            // User must never be fetched for an invalid token
            _userRepoMock.Verify(
                r => r.GetByIdAsync(It.IsAny<int>()),
                Times.Never);

            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);
        }

        // Test 4: Expired refresh token
        [Fact]
        [Trait("Category", "RefreshAccessTokenAsync")]
        public async Task RefreshAccessTokenAsync_WithExpiredToken_ShouldReturnNull()
        {
            // ======== Arrange ========

            // expiresAt in the past → IsExpired = true → IsActive = false
            // ExpiresAt = يوم في الماضي
            // IsExpired = DateTime.UtcNow >= ExpiresAt = true
            // IsActive  = !false && !true = false
            var expiredToken = CreateFakeRefreshToken(
                expiresAt: DateTime.UtcNow.AddDays(-1));

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { expiredToken });

            // ======== Act ========

            var result = await _sut.RefreshAccessTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // IsActive = false because token is expired → return null
            result.Should().BeNull();

            // User must never be fetched for an expired token
            _userRepoMock.Verify(
                r => r.GetByIdAsync(It.IsAny<int>()),
                Times.Never);

            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);
        }

        // Test 5: Valid token but inactive user
        [Fact]
        [Trait("Category", "RefreshAccessTokenAsync")]
        public async Task RefreshAccessTokenAsync_WithValidTokenButInactiveUser_ShouldReturnNull()
        {
            // ======== Arrange ========

            var existingToken = CreateFakeRefreshToken();
            // التوكن صالح — المشكلة في المستخدم
            var inactiveUser = CreateFakeUser(isActive: false);

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { existingToken });

            // User exists but is disabled
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(inactiveUser);

            // ======== Act ========

            var result = await _sut.RefreshAccessTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // Token is valid but user is inactive → return null
            result.Should().BeNull();

            // No Access Token should be generated for inactive users
            _jwtTokenServiceMock.Verify(
                j => j.GenerateToken(It.IsAny<User>()),
                Times.Never);

            // Nothing should be saved to DB
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Never);
        }

        // ============================================
        // RevokeRefreshTokenAsync Tests
        // ============================================

        // Test 1: Valid active token
        [Fact]
        [Trait("Category", "RevokeRefreshTokenAsync")]
        public async Task RevokeRefreshTokenAsync_WithValidToken_ShouldRevokeAndReturnTrue()
        {
            // ======== Arrange ========

            // Active token: IsRevoked = false + not expired
            var existingToken = CreateFakeRefreshToken();

            // When searching for the token → return it
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { existingToken });

            // ======== Act ========

            var result = await _sut.RevokeRefreshTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // Must return true → revocation succeeded
            result.Should().BeTrue();

            // UpdateAsync must be called with IsRevoked=true AND RevokedAt set
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.Is<RefreshToken>(t =>
                    t.IsRevoked == true &&
                    t.RevokedAt != null)),
                Times.Once);

            // SaveChangesAsync must be called to persist the revocation
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Once);
        }

        // Test 2: Non-existent token
        [Fact]
        [Trait("Category", "RevokeRefreshTokenAsync")]
        public async Task RevokeRefreshTokenAsync_WithNonExistentToken_ShouldReturnFalse()
        {
            // ======== Arrange ========

            // Search returns empty list → token not found
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken>());

            // ======== Act ========

            var result = await _sut.RevokeRefreshTokenAsync("non_existent_token");

            // ======== Assert ========

            // Must return false → nothing to revoke
            result.Should().BeFalse();

            // UpdateAsync must never be called → nothing to update
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.IsAny<RefreshToken>()),
                Times.Never);

            // SaveChangesAsync must never be called → no changes made
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Never);
        }

        // Test 3: Already revoked token
        [Fact]
        [Trait("Category", "RevokeRefreshTokenAsync")]
        public async Task RevokeRefreshTokenAsync_WithAlreadyRevokedToken_ShouldReturnFalse()
        {
            // ======== Arrange ========

            // isRevoked: true → already revoked before
            var alreadyRevokedToken = CreateFakeRefreshToken(isRevoked: true);

            // Token exists in DB but is already revoked
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { alreadyRevokedToken });

            // ======== Act ========

            var result = await _sut.RevokeRefreshTokenAsync("fake_refresh_token_string");

            // ======== Assert ========

            // Must return false → already revoked, nothing to do
            result.Should().BeFalse();

            // UpdateAsync must never be called → no need to update again
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.IsAny<RefreshToken>()),
                Times.Never);

            // SaveChangesAsync must never be called → no changes made
            _unitOfWorkMock.Verify(
                u => u.SaveChangesAsync(),
                Times.Never);
        }

    }
}