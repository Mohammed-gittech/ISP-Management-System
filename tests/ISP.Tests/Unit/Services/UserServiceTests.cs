// ============================================
// UserServiceTests.cs
// Unit Tests for UserService
// ============================================

using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using ISP.Application.DTOs.Users;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ISP.Tests.Unit.Services
{
    public class UserServiceTests
    {
        // ============================================
        // Mocks
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<ICurrentTenantService> _currentTenantMock;
        private readonly Mock<ILogger<UserService>> _loggerMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IRepository<Tenant>> _tenantRepoMock;
        private readonly Mock<IRepository<RefreshToken>> _refreshTokenRepoMock;
        // ← RefreshToken Mock: مطلوب لـ RevokeAllUserTokensAsync

        private readonly UserService _service;

        private const int TenantId = 1;
        private const int UserId = 10;

        // ============================================
        // Constructor — يُنفَّذ قبل كل اختبار
        // ============================================

        public UserServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _passwordHasherMock = new Mock<IPasswordHasher>();
            _currentTenantMock = new Mock<ICurrentTenantService>();
            _loggerMock = new Mock<ILogger<UserService>>();
            _userRepoMock = new Mock<IRepository<User>>();
            _tenantRepoMock = new Mock<IRepository<Tenant>>();
            _refreshTokenRepoMock = new Mock<IRepository<RefreshToken>>();

            _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.RefreshTokens).Returns(_refreshTokenRepoMock.Object);

            // UserId الحالي = 99 — مختلف عن UserId = 10 لتجنب تعارض اختبارات حذف النفس
            _currentTenantMock.Setup(c => c.UserId).Returns(99);
            _currentTenantMock.Setup(c => c.TenantId).Returns(TenantId);

            // الافتراضي: لا توجد Refresh Tokens نشطة
            // أي اختبار يحتاج tokens سيعدّل هذا الـ Setup
            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken>());

            _service = new UserService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _passwordHasherMock.Object,
                _currentTenantMock.Object,
                _loggerMock.Object
            );
        }

        // ============================================
        // Helper Methods
        // ============================================

        private User CreateFakeUser(
            int id = UserId,
            UserRole role = UserRole.Employee,
            bool isActive = true,
            bool isDeleted = false) => new User
            {
                Id = id,
                TenantId = role == UserRole.SuperAdmin ? null : TenantId,
                Username = $"user_{id}",
                Email = $"user{id}@alnoor.com",
                PasswordHash = "hashed_password",
                Role = role,
                IsActive = isActive,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow.AddDays(-id)
            };

        private UserDto CreateFakeUserDto(
            int id = UserId,
            string role = "Employee",
            bool isActive = true) => new UserDto
            {
                Id = id,
                TenantId = TenantId,
                TenantName = "شركة النور",
                Username = $"user_{id}",
                Email = $"user{id}@alnoor.com",
                Role = role,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            };

        private CreateUserDto CreateFakeCreateDto(string role = "Employee") => new CreateUserDto
        {
            TenantId = role == "SuperAdmin" ? null : TenantId,
            Username = "ahmed_admin",
            Email = "ahmed@alnoor.com",
            Password = "Admin@123",
            Role = role
        };

        private RefreshToken CreateFakeRefreshToken(int id = 1) => new RefreshToken
        {
            Id = id,
            UserId = UserId,
            Token = $"fake_token_{id}",
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        // ============================================
        // GetAllAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "GetAllAsync")]
        public async Task GetAllAsync_WithNoSearch_ShouldReturnPagedResult()
        {
            // Arrange
            var users = new List<User> { CreateFakeUser(1), CreateFakeUser(2), CreateFakeUser(3) };
            var userDtos = users.Select(u => CreateFakeUserDto(u.Id)).ToList();

            _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(userDtos);
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // Act
            var result = await _service.GetAllAsync(pageNumber: 1, pageSize: 10);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(3);
            result.Items.Should().HaveCount(3);
            result.PageNumber.Should().Be(1);
            result.PageSize.Should().Be(10);
        }

        [Fact]
        [Trait("Category", "GetAllAsync")]
        public async Task GetAllAsync_WithSearchTerm_ShouldCallFilteredQuery()
        {
            // Arrange
            var matchingUsers = new List<User> { CreateFakeUser(1) };
            var userDtos = new List<UserDto> { CreateFakeUserDto(1) };

            // عند وجود searchTerm يستدعي GetAllAsync(predicate) وليس GetAllAsync()
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(matchingUsers);
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(userDtos);
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // Act
            var result = await _service.GetAllAsync(pageNumber: 1, pageSize: 10, searchTerm: "ahmed");

            // Assert
            result.TotalCount.Should().Be(1);
            // يجب أن يستدعي GetAllAsync بـ predicate وليس بدونها
            _userRepoMock.Verify(
                r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()),
                Times.Once);
            _userRepoMock.Verify(r => r.GetAllAsync(), Times.Never);
        }

        [Fact]
        [Trait("Category", "GetAllAsync")]
        public async Task GetAllAsync_WithPagination_ShouldReturnCorrectPage()
        {
            // Arrange — 15 مستخدم، نطلب الصفحة الثانية بحجم 10
            var users = Enumerable.Range(1, 15).Select(i => CreateFakeUser(i)).ToList();

            _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(users);
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns((List<User> u) => u.Select(x => CreateFakeUserDto(x.Id)).ToList());
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // Act
            var result = await _service.GetAllAsync(pageNumber: 2, pageSize: 10);

            // Assert
            result.TotalCount.Should().Be(15); // الإجمالي ثابت
            result.Items.Should().HaveCount(5); // الصفحة الثانية = 5 فقط
            result.PageNumber.Should().Be(2);
        }

        [Fact]
        [Trait("Category", "GetAllAsync")]
        public async Task GetAllAsync_WhenNoUsers_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(new List<UserDto>());

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        // ============================================
        // GetUsersByTenantAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "GetUsersByTenantAsync")]
        public async Task GetUsersByTenantAsync_WhenTenantHasUsers_ShouldReturnPagedResult()
        {
            // Arrange
            var tenantUsers = new List<User> { CreateFakeUser(1), CreateFakeUser(2) };
            var userDtos = tenantUsers.Select(u => CreateFakeUserDto(u.Id)).ToList();

            _userRepoMock
                .Setup(r => r.GetByTenantAsync(TenantId))
                .ReturnsAsync(tenantUsers);
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(userDtos);

            // Act
            var result = await _service.GetUsersByTenantAsync(TenantId, pageNumber: 1, pageSize: 10);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);

            _userRepoMock.Verify(r => r.GetByTenantAsync(TenantId), Times.Once);
        }

        [Fact]
        [Trait("Category", "GetUsersByTenantAsync")]
        public async Task GetUsersByTenantAsync_WhenTenantHasNoUsers_ShouldReturnEmptyResult()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByTenantAsync(TenantId))
                .ReturnsAsync(new List<User>());
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(new List<UserDto>());

            // Act
            var result = await _service.GetUsersByTenantAsync(TenantId, pageNumber: 1, pageSize: 10);

            // Assert
            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        // ============================================
        // GetByIdAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "GetByIdAsync")]
        public async Task GetByIdAsync_WhenUserExists_ShouldReturnUserDtoWithTenantName()
        {
            // Arrange
            var user = CreateFakeUser();
            var expectedDto = CreateFakeUserDto();

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(expectedDto);

            // Act
            var result = await _service.GetByIdAsync(UserId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(UserId);
            result.TenantName.Should().Be("شركة النور");
        }

        [Fact]
        [Trait("Category", "GetByIdAsync")]
        public async Task GetByIdAsync_WhenUserNotFound_ShouldReturnNull()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();
            _tenantRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ============================================
        // CreateAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateAsync")]
        public async Task CreateAsync_WithValidData_ShouldReturnUserDto()
        {
            // Arrange
            var dto = CreateFakeCreateDto();
            var user = CreateFakeUser();
            var expectedDto = CreateFakeUserDto();

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.Password))
                .Returns("hashed_password");
            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => { u.Id = UserId; return u; });
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(expectedDto);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(UserId);
            result.Role.Should().Be("Employee");

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
            _passwordHasherMock.Verify(p => p.HashPassword(dto.Password), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "CreateAsync")]
        public async Task CreateAsync_WithDuplicateEmail_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { CreateFakeUser() });

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*البريد الإلكتروني مستخدم مسبقًا*");

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "CreateAsync")]
        public async Task CreateAsync_WithDuplicateUsername_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();

            _userRepoMock
                .SetupSequence(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>())
                .ReturnsAsync(new List<User> { CreateFakeUser() });

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*اسم المستخدم مستخدم مسبقًا*");
        }

        [Fact]
        [Trait("Category", "CreateAsync")]
        public async Task CreateAsync_WithInvalidRole_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();
            dto.Role = "Manager";

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.Password))
                .Returns("hashed_password");

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الدور غير صحيح*");
        }

        [Fact]
        [Trait("Category", "CreateAsync")]
        public async Task CreateAsync_SuperAdminWithNullTenantId_ShouldReturnUserDto()
        {
            // Arrange
            var dto = CreateFakeCreateDto("SuperAdmin");
            var user = CreateFakeUser(role: UserRole.SuperAdmin);
            var expectedDto = CreateFakeUserDto(role: "SuperAdmin");
            expectedDto.TenantId = null;

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.Password))
                .Returns("hashed_password");
            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => { u.Id = UserId; return u; });
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(expectedDto);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.TenantId.Should().BeNull();
            result.Role.Should().Be("SuperAdmin");
        }

        // ============================================
        // UpdateAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "UpdateAsync")]
        public async Task UpdateAsync_WithValidData_ShouldUpdateAndReturnUserDto()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new UpdateUserDto { Username = "ahmed_new", Email = "ahmed_new@alnoor.com", IsActive = true };
            var expectedDto = CreateFakeUserDto();
            expectedDto.Username = "ahmed_new";

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(expectedDto);

            // Act
            var result = await _service.UpdateAsync(UserId, dto);

            // Assert
            result.Should().NotBeNull();
            user.Username.Should().Be("ahmed_new");
            user.Email.Should().Be("ahmed_new@alnoor.com");

            _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "UpdateAsync")]
        public async Task UpdateAsync_WhenUserNotFound_ShouldReturnNull()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.UpdateAsync(999, new UpdateUserDto { Username = "new_name" });

            // Assert
            result.Should().BeNull();
            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "UpdateAsync")]
        public async Task UpdateAsync_WithDuplicateUsername_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new UpdateUserDto { Username = "existing_user" };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { CreateFakeUser(id: 55) });

            // Act
            var act = async () => await _service.UpdateAsync(UserId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*اسم المستخدم مستخدم مسبقًا*");

            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "UpdateAsync")]
        public async Task UpdateAsync_WithNullFields_ShouldNotOverwriteExistingValues()
        {
            // Arrange
            var user = CreateFakeUser();
            var originalEmail = user.Email;
            var dto = new UpdateUserDto { Username = "new_name_only" };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(CreateFakeUserDto());

            // Act
            await _service.UpdateAsync(UserId, dto);

            // Assert
            user.Username.Should().Be("new_name_only");
            user.Email.Should().Be(originalEmail); // الإيميل لم يتغيّر
        }

        // ============================================
        // DeleteAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "DeleteAsync")]
        public async Task DeleteAsync_WhenUserExists_ShouldSoftDeleteAndReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _currentTenantMock.Setup(c => c.UserId).Returns(99);
            _userRepoMock.Setup(r => r.SoftDeleteAsync(user)).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.DeleteAsync(UserId);

            // Assert
            result.Should().BeTrue();
            _userRepoMock.Verify(r => r.SoftDeleteAsync(user), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "DeleteAsync")]
        public async Task DeleteAsync_WhenUserExists_ShouldRevokeAllRefreshTokens()
        {
            // Arrange — المستخدم لديه 2 توكن نشطان
            var user = CreateFakeUser();
            var token1 = CreateFakeRefreshToken(1);
            var token2 = CreateFakeRefreshToken(2);

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _currentTenantMock.Setup(c => c.UserId).Returns(99);

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { token1, token2 });
            _refreshTokenRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);
            _userRepoMock.Setup(r => r.SoftDeleteAsync(user)).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.DeleteAsync(UserId);

            // Assert — كلا التوكنين يجب أن يُلغَيا
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.Is<RefreshToken>(t => t.IsRevoked == true && t.RevokedAt != null)),
                Times.Exactly(2));
        }

        [Fact]
        [Trait("Category", "DeleteAsync")]
        public async Task DeleteAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.DeleteAsync(999);

            // Assert
            result.Should().BeFalse();
            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "DeleteAsync")]
        public async Task DeleteAsync_WhenLastSuperAdmin_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var superAdmin = CreateFakeUser(role: UserRole.SuperAdmin);

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(superAdmin);
            _userRepoMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(new List<User> { superAdmin });

            // Act
            var act = async () => await _service.DeleteAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*لا يمكن حذف آخر SuperAdmin*");

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "DeleteAsync")]
        public async Task DeleteAsync_WhenDeletingSelf_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _currentTenantMock.Setup(c => c.UserId).Returns(UserId); // يحذف نفسه

            // Act
            var act = async () => await _service.DeleteAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*لا يمكنك حذف نفسك*");

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<User>()), Times.Never);
        }

        // ============================================
        // GetDeletedAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "GetDeletedAsync")]
        public async Task GetDeletedAsync_WhenDeletedUsersExist_ShouldReturnPagedResult()
        {
            // Arrange
            var deletedUsers = new List<User>
            {
                CreateFakeUser(1, isDeleted: true),
                CreateFakeUser(2, isDeleted: true)
            };
            var userDtos = deletedUsers.Select(u => CreateFakeUserDto(u.Id)).ToList();

            _userRepoMock.Setup(r => r.GetDeletedAsync()).ReturnsAsync(deletedUsers);
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(userDtos);
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });

            // Act
            var result = await _service.GetDeletedAsync(pageNumber: 1, pageSize: 10);

            // Assert
            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);

            _userRepoMock.Verify(r => r.GetDeletedAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "GetDeletedAsync")]
        public async Task GetDeletedAsync_WhenNoDeletedUsers_ShouldReturnEmptyPagedResult()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetDeletedAsync()).ReturnsAsync(new List<User>());
            _mapperMock
                .Setup(m => m.Map<List<UserDto>>(It.IsAny<List<User>>()))
                .Returns(new List<UserDto>());

            // Act
            var result = await _service.GetDeletedAsync();

            // Assert
            result.TotalCount.Should().Be(0);
            result.Items.Should().BeEmpty();
        }

        // ============================================
        // RestoreAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "RestoreAsync")]
        public async Task RestoreAsync_WhenUserIsDeleted_ShouldRestoreAndReturnTrue()
        {
            // Arrange
            var deletedUser = CreateFakeUser(isDeleted: true);

            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(UserId)).ReturnsAsync(deletedUser);
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());
            _userRepoMock.Setup(r => r.RestoreByIdAsync(UserId)).ReturnsAsync(true);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.RestoreAsync(UserId);

            // Assert
            result.Should().BeTrue();
            _userRepoMock.Verify(r => r.RestoreByIdAsync(UserId), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "RestoreAsync")]
        public async Task RestoreAsync_WhenUserIsNotDeleted_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(CreateFakeUser(isDeleted: false));

            // Act
            var result = await _service.RestoreAsync(UserId);

            // Assert
            result.Should().BeFalse();
            _userRepoMock.Verify(r => r.RestoreByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "RestoreAsync")]
        public async Task RestoreAsync_WhenEmailDuplicate_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var deletedUser = CreateFakeUser(isDeleted: true);

            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(UserId)).ReturnsAsync(deletedUser);
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { CreateFakeUser(id: 55) });

            // Act
            var act = async () => await _service.RestoreAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*البريد الإلكتروني*");

            _userRepoMock.Verify(r => r.RestoreByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "RestoreAsync")]
        public async Task RestoreAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.RestoreAsync(999);

            // Assert
            result.Should().BeFalse();
            _userRepoMock.Verify(r => r.RestoreByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ============================================
        // PermanentDeleteAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "PermanentDeleteAsync")]
        public async Task PermanentDeleteAsync_WhenUserIsSoftDeleted_ShouldDeleteAndReturnTrue()
        {
            // Arrange — المستخدم محذوف Soft → يُسمح بالحذف النهائي
            var deletedUser = CreateFakeUser(isDeleted: true);

            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(deletedUser);
            _userRepoMock
                .Setup(r => r.DeleteAsync(deletedUser))
                .Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.PermanentDeleteAsync(UserId);

            // Assert
            result.Should().BeTrue();
            _userRepoMock.Verify(r => r.DeleteAsync(deletedUser), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "PermanentDeleteAsync")]
        public async Task PermanentDeleteAsync_WhenUserIsActive_ShouldThrowInvalidOperationException()
        {
            // Arrange — المستخدم نشط → يجب Soft Delete أولاً
            var activeUser = CreateFakeUser(isDeleted: false);

            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(activeUser);

            // Act
            var act = async () => await _service.PermanentDeleteAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*استخدم Soft Delete أولاً*");

            _userRepoMock.Verify(r => r.DeleteAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "PermanentDeleteAsync")]
        public async Task PermanentDeleteAsync_WhenUserIsSuperAdmin_ShouldThrowInvalidOperationException()
        {
            // Arrange — SuperAdmin محمي حتى لو محذوف Soft
            var superAdmin = CreateFakeUser(role: UserRole.SuperAdmin, isDeleted: true);

            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(superAdmin);

            // Act
            var act = async () => await _service.PermanentDeleteAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*لا يمكن الحذف النهائي لحساب SuperAdmin*");

            _userRepoMock.Verify(r => r.DeleteAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "PermanentDeleteAsync")]
        public async Task PermanentDeleteAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.PermanentDeleteAsync(999);

            // Assert
            result.Should().BeFalse();
            _userRepoMock.Verify(r => r.DeleteAsync(It.IsAny<User>()), Times.Never);
        }

        // ============================================
        // ChangePasswordAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordAsync")]
        public async Task ChangePasswordAsync_WithCorrectOldPassword_ShouldReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new ChangePasswordDto
            {
                OldPassword = "OldPass@123",
                NewPassword = "NewPass@123",
                ConfirmPassword = "NewPass@123"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(dto.OldPassword, user.PasswordHash))
                .Returns(true);
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.NewPassword))
                .Returns("new_hashed_password");
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.ChangePasswordAsync(UserId, dto);

            // Assert
            result.Should().BeTrue();
            user.PasswordHash.Should().Be("new_hashed_password");

            _passwordHasherMock.Verify(p => p.HashPassword(dto.NewPassword), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "ChangePasswordAsync")]
        public async Task ChangePasswordAsync_WithCorrectOldPassword_ShouldRevokeAllRefreshTokens()
        {
            // Arrange
            var user = CreateFakeUser();
            var token = CreateFakeRefreshToken();
            var dto = new ChangePasswordDto
            {
                OldPassword = "OldPass@123",
                NewPassword = "NewPass@123",
                ConfirmPassword = "NewPass@123"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(dto.OldPassword, user.PasswordHash))
                .Returns(true);
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.NewPassword))
                .Returns("new_hashed_password");

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { token });
            _refreshTokenRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.ChangePasswordAsync(UserId, dto);

            // Assert
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.Is<RefreshToken>(t => t.IsRevoked == true && t.RevokedAt != null)),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ChangePasswordAsync")]
        public async Task ChangePasswordAsync_WithWrongOldPassword_ShouldThrowAndNotRevoke()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new ChangePasswordDto { OldPassword = "WrongPass@123", NewPassword = "NewPass@123" };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(dto.OldPassword, user.PasswordHash))
                .Returns(false);

            // Act
            var act = async () => await _service.ChangePasswordAsync(UserId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*كلمة المرور القديمة غير صحيحة*");

            _passwordHasherMock.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
            _refreshTokenRepoMock.Verify(r => r.UpdateAsync(It.IsAny<RefreshToken>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "ChangePasswordAsync")]
        public async Task ChangePasswordAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.ChangePasswordAsync(
                999,
                new ChangePasswordDto { OldPassword = "Old", NewPassword = "New" });

            // Assert
            result.Should().BeFalse();
            _passwordHasherMock.Verify(
                p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        // ============================================
        // ResetPasswordAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "ResetPasswordAsync")]
        public async Task ResetPasswordAsync_WhenUserExists_ShouldReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new ResetPasswordDto
            {
                NewPassword = "NewPass@123",
                ConfirmPassword = "NewPass@123"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.NewPassword))
                .Returns("reset_hashed_password");
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.ResetPasswordAsync(UserId, dto);

            // Assert
            result.Should().BeTrue();
            user.PasswordHash.Should().Be("reset_hashed_password");

            _passwordHasherMock.Verify(p => p.HashPassword(dto.NewPassword), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "ResetPasswordAsync")]
        public async Task ResetPasswordAsync_WhenUserExists_ShouldRevokeAllRefreshTokens()
        {
            // Arrange
            var user = CreateFakeUser();
            var token = CreateFakeRefreshToken();
            var dto = new ResetPasswordDto
            {
                NewPassword = "NewPass@123",
                ConfirmPassword = "NewPass@123"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.NewPassword))
                .Returns("reset_hashed_password");

            _refreshTokenRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>()))
                .ReturnsAsync(new List<RefreshToken> { token });
            _refreshTokenRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<RefreshToken>()))
                .Returns(Task.CompletedTask);
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            await _service.ResetPasswordAsync(UserId, dto);

            // Assert
            _refreshTokenRepoMock.Verify(
                r => r.UpdateAsync(It.Is<RefreshToken>(t => t.IsRevoked == true && t.RevokedAt != null)),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ResetPasswordAsync")]
        public async Task ResetPasswordAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            var dto = new ResetPasswordDto { NewPassword = "NewPass@123", ConfirmPassword = "NewPass@123" };

            // Act
            var result = await _service.ResetPasswordAsync(999, dto);

            // Assert
            result.Should().BeFalse();
            _passwordHasherMock.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
            _refreshTokenRepoMock.Verify(r => r.UpdateAsync(It.IsAny<RefreshToken>()), Times.Never);
        }

        // ============================================
        // AssignRoleAsync Tests
        // ============================================

        [Fact]
        [Trait("Category", "AssignRoleAsync")]
        public async Task AssignRoleAsync_WithValidRole_ShouldAssignAndReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser(role: UserRole.Employee);

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);
            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.AssignRoleAsync(UserId, "TenantAdmin");

            // Assert
            result.Should().BeTrue();
            user.Role.Should().Be(UserRole.TenantAdmin);

            _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "AssignRoleAsync")]
        public async Task AssignRoleAsync_WithInvalidRole_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();
            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // Act
            var act = async () => await _service.AssignRoleAsync(UserId, "Manager");

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*الدور غير صحيح*");

            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "AssignRoleAsync")]
        public async Task AssignRoleAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.AssignRoleAsync(999, "TenantAdmin");

            // Assert
            result.Should().BeFalse();
            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }
    }
}