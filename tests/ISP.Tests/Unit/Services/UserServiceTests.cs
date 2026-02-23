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
        // Mocks — نسخ وهمية بدل قاعدة البيانات الحقيقية
        // ============================================

        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IPasswordHasher> _passwordHasherMock;
        private readonly Mock<ICurrentTenantService> _currentTenantMock;
        private readonly Mock<ILogger<UserService>> _loggerMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IRepository<Tenant>> _tenantRepoMock;

        // SUT — الكود الحقيقي الذي نختبره
        private readonly UserService _service;

        // ثوابت لتجنب تكرار الأرقام في كل اختبار
        private const int TenantId = 1;
        private const int UserId = 10;


        // ============================================
        // Constructor — يُنفَّذ تلقائياً قبل كل اختبار
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

            // ربط كل Repository بالـ UnitOfWork
            _unitOfWorkMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
            _unitOfWorkMock.Setup(u => u.Tenants).Returns(_tenantRepoMock.Object);

            // المستخدم الحالي هو UserId = 99 (مختلف عن UserId = 10)
            // حتى لا يتعارض مع اختبارات حذف النفس
            _currentTenantMock.Setup(c => c.UserId).Returns(99);
            _currentTenantMock.Setup(c => c.TenantId).Returns(TenantId);

            // إنشاء الـ Service الحقيقي بالـ Mocks
            _service = new UserService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _passwordHasherMock.Object,
                _currentTenantMock.Object,
                _loggerMock.Object
            );
        }

        // ============================================
        // Helper Methods — بيانات وهمية جاهزة للاستخدام
        // ============================================

        // ينشئ User وهمي — role تتحكم في الدور
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
                CreatedAt = DateTime.UtcNow
            };

        // ينشئ UserDto وهمي للرد
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

        // ينشئ CreateUserDto وهمي — role تتحكم في الدور
        private CreateUserDto CreateFakeCreateDto(string role = "Employee") => new CreateUserDto
        {
            TenantId = role == "SuperAdmin" ? null : TenantId,
            Username = "ahmed_admin",
            Email = "ahmed@alnoor.com",
            Password = "Admin@123",
            Role = role
        };

        // ============================================
        // CreateAsync Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة — يجب أن ينشئ المستخدم ويرجع UserDto
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithValidData_ShouldReturnUserDto()
        {
            // Arrange
            var dto = CreateFakeCreateDto();
            var user = CreateFakeUser();
            var expectedDto = CreateFakeUserDto();

            // لا يوجد Email مكرر
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            // تشفير كلمة المرور
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.Password))
                .Returns("hashed_password");

            // إضافة المستخدم
            _userRepoMock
                .Setup(r => r.AddAsync(It.IsAny<User>()))
                .ReturnsAsync((User u) => { u.Id = UserId; return u; });

            _unitOfWorkMock
                .Setup(u => u.SaveChangesAsync())
                .ReturnsAsync(1);

            // GetByIdAsync الذي يُستدعى داخل CreateAsync
            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(user);

            _mapperMock
                .Setup(m => m.Map<UserDto>(user))
                .Returns(expectedDto);

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

        // الاختبار الثاني: Email مكرر — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithDuplicateEmail_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();

            // نرجع مستخدم موجود لمحاكاة تكرار الـ Email
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User> { CreateFakeUser() });

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*البريد الإلكتروني مستخدم مسبقًا*");

            // لا يجب أن يُستدعى AddAsync
            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        // الاختبار الثالث: Username مكرر — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithDuplicateUsername_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();

            // Email فريد — لكن Username مكرر
            _userRepoMock
                .SetupSequence(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>())              // Email فريد
                .ReturnsAsync(new List<User> { CreateFakeUser() }); // Username مكرر

            // Act
            var act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*اسم المستخدم مستخدم مسبقًا*");
        }

        // الاختبار الرابع: Role غير صحيح — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task CreateAsync_WithInvalidRole_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dto = CreateFakeCreateDto();
            dto.Role = "Manager"; // دور غير موجود في الـ Enum

            // Email و Username فريدان
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

        // الاختبار الخامس: SuperAdmin بدون TenantId — يجب أن ينجح
        [Fact]
        [Trait("Category", "Service")]
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
            result.Should().NotBeNull();
            result.TenantId.Should().BeNull(); // SuperAdmin لا يملك TenantId
            result.Role.Should().Be("SuperAdmin");
        }

        // ============================================
        // GetByIdAsync Tests
        // ============================================

        // UserDto الاختبار الأول: مستخدم موجود — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenUserExists_ShouldReturnUserDto()
        {
            // Arrange
            var user = CreateFakeUser();
            var expectedDto = CreateFakeUserDto();
            var tenant = new Tenant { Id = TenantId, Name = "شركة النور" };

            _userRepoMock
                .Setup(r => r.GetByIdAsync(UserId))
                .ReturnsAsync(user);

            // يجلب اسم الوكيل لإضافته للـ Dto
            _tenantRepoMock
                .Setup(r => r.GetByIdAsync(TenantId))
                .ReturnsAsync(tenant);

            _mapperMock
                .Setup(m => m.Map<UserDto>(user))
                .Returns(expectedDto);

            // Act
            var result = await _service.GetByIdAsync(UserId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(UserId);
            result.TenantName.Should().Be("شركة النور");
        }

        // null الاختبار الثاني: مستخدم غير موجود — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task GetByIdAsync_WhenUserNotFound_ShouldReturnNull()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.GetByIdAsync(999);

            // Assert
            result.Should().BeNull();

            // لا يجب أن يستدعي Tenants لأن المستخدم غير موجود
            _tenantRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // ============================================
        // UpdateAsync Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة — يجب أن يحدّث ويحفظ
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WithValidData_ShouldUpdateAndReturnUserDto()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new UpdateUserDto
            {
                Username = "ahmed_new",
                Email = "ahmed_new@alnoor.com",
                IsActive = true
            };

            var expectedDto = CreateFakeUserDto();
            expectedDto.Username = "ahmed_new";

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // لا يوجد تكرار في Username و Email
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });
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


        // null الاختبار الثاني: مستخدم غير موجود — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WhenUserNotFound_ShouldReturnNull()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            var dto = new UpdateUserDto { Username = "new_name" };

            // Act
            var result = await _service.UpdateAsync(999, dto);

            // Assert
            result.Should().BeNull();

            // لا يجب أن يُستدعى UpdateAsync
            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        // الاختبار الثالث: Username مكرر — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WithDuplicateUsername_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new UpdateUserDto { Username = "existing_user" };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // Username موجود لمستخدم آخر
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

        // الاختبار الرابع: الحقول الفارغة لا تُحدَّث
        [Fact]
        [Trait("Category", "Service")]
        public async Task UpdateAsync_WithNullFields_ShouldNotOverwriteExistingValues()
        {
            // Arrange
            var user = CreateFakeUser();
            var originalEmail = user.Email;

            // dto لا يحتوي على Email — يجب أن يبقى كما هو
            var dto = new UpdateUserDto { Username = "new_name_only" };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
            _tenantRepoMock.Setup(r => r.GetByIdAsync(TenantId)).ReturnsAsync(new Tenant { Id = TenantId, Name = "شركة النور" });
            _mapperMock.Setup(m => m.Map<UserDto>(user)).Returns(CreateFakeUserDto());

            // Act
            await _service.UpdateAsync(UserId, dto);

            // Assert
            user.Username.Should().Be("new_name_only"); // الاسم تغيّر
            user.Email.Should().Be(originalEmail);       // الإيميل لم يتغيّر
        }

        // ============================================
        // DeleteAsync Tests
        // ============================================

        // true الاختبار الأول: مستخدم موجود — يجب أن يُحذف ويرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeleteAsync_WhenUserExists_ShouldSoftDeleteAndReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // المستخدم الحالي هو 99 — مختلف عن UserId = 10
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

        // false الاختبار الثاني: مستخدم غير موجود — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeleteAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.DeleteAsync(999);

            // Assert
            result.Should().BeFalse();

            // لا يجب أن يُستدعى SoftDelete
            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<User>()), Times.Never);
        }

        // يجب أن يرمي استثنا SuperAdmin الاختبار الثالث: حذف آخر 
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeleteAsync_WhenLastSuperAdmin_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var superAdmin = CreateFakeUser(role: UserRole.SuperAdmin);

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(superAdmin);

            // يوجد SuperAdmin واحد فقط في النظام
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

        // الاختبار الرابع: المستخدم يحذف نفسه — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task DeleteAsync_WhenDeletingSelf_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // المستخدم الحالي هو نفسه UserId = 10
            _currentTenantMock.Setup(c => c.UserId).Returns(UserId);

            // Act
            var act = async () => await _service.DeleteAsync(UserId);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*لا يمكنك حذف نفسك*");

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<User>()), Times.Never);
        }

        // ============================================
        // RestoreAsync Tests
        // ============================================

        // true الاختبار الأول: مستخدم محذوف — يجب أن يُسترجع ويرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task RestoreAsync_WhenUserIsDeleted_ShouldRestoreAndReturnTrue()
        {
            // Arrange
            var deletedUser = CreateFakeUser(isDeleted: true);

            // GetByIdIncludingDeletedAsync يجلب المحذوفين أيضاً
            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(deletedUser);

            // لا يوجد Email أو Username مكرر في نفس الـ Tenant
            _userRepoMock
                .Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<User, bool>>>()))
                .ReturnsAsync(new List<User>());

            _userRepoMock
                .Setup(r => r.RestoreByIdAsync(UserId))
                .ReturnsAsync(true);

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.RestoreAsync(UserId);

            // Assert
            result.Should().BeTrue();

            _userRepoMock.Verify(r => r.RestoreByIdAsync(UserId), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // false الاختبار الثاني: مستخدم غير محذوف — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
        public async Task RestoreAsync_WhenUserIsNotDeleted_ShouldReturnFalse()
        {
            // Arrange
            // المستخدم موجود لكنه غير محذوف
            var activeUser = CreateFakeUser(isDeleted: false);

            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(activeUser);

            // Act
            var result = await _service.RestoreAsync(UserId);

            // Assert
            result.Should().BeFalse();

            // لا يجب أن يُستدعى RestoreByIdAsync
            _userRepoMock.Verify(r => r.RestoreByIdAsync(It.IsAny<int>()), Times.Never);
        }

        // الاختبار الثالث: Email مكرر في نفس الـ Tenant — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task RestoreAsync_WhenEmailDuplicateInSameTenant_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var deletedUser = CreateFakeUser(isDeleted: true);

            _userRepoMock
                .Setup(r => r.GetByIdIncludingDeletedAsync(UserId))
                .ReturnsAsync(deletedUser);

            // يوجد مستخدم آخر بنفس الـ Email في نفس الـ Tenant
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

        // false الاختبار الرابع: مستخدم غير موجود — يجب أن يرجع 
        [Fact]
        [Trait("Category", "Service")]
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
        // ChangePasswordAsync Tests
        // ============================================

        // الاختبار الأول: كلمة المرور القديمة صحيحة — يجب أن يغيّر ويرجع true
        [Fact]
        [Trait("Category", "Service")]
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

            // كلمة المرور القديمة صحيحة
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(dto.OldPassword, user.PasswordHash))
                .Returns(true);

            // تشفير كلمة المرور الجديدة
            _passwordHasherMock
                .Setup(p => p.HashPassword(dto.NewPassword))
                .Returns("new_hashed_password");

            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.ChangePasswordAsync(UserId, dto);

            // Assert
            result.Should().BeTrue();
            user.PasswordHash.Should().Be("new_hashed_password"); // كلمة المرور تغيّرت

            _passwordHasherMock.Verify(p => p.HashPassword(dto.NewPassword), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: كلمة المرور القديمة خاطئة — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
        public async Task ChangePasswordAsync_WithWrongOldPassword_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var user = CreateFakeUser();
            var dto = new ChangePasswordDto
            {
                OldPassword = "WrongPass@123",
                NewPassword = "NewPass@123"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            // كلمة المرور القديمة خاطئة
            _passwordHasherMock
                .Setup(p => p.VerifyPassword(dto.OldPassword, user.PasswordHash))
                .Returns(false);

            // Act
            var act = async () => await _service.ChangePasswordAsync(UserId, dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*كلمة المرور القديمة غير صحيحة*");

            // لا يجب أن يُستدعى HashPassword
            _passwordHasherMock.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        }

        // الاختبار الثالث: مستخدم غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ChangePasswordAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            var dto = new ChangePasswordDto { OldPassword = "Old", NewPassword = "New" };

            // Act
            var result = await _service.ChangePasswordAsync(999, dto);

            // Assert
            result.Should().BeFalse();

            _passwordHasherMock.Verify(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        // ============================================
        // ResetPasswordAsync Tests
        // ============================================

        // الاختبار الأول: مستخدم موجود — يجب أن يعيد تعيين كلمة المرور
        [Fact]
        [Trait("Category", "Service")]
        public async Task ResetPasswordAsync_WhenUserExists_ShouldReturnTrue()
        {
            // Arrange
            var user = CreateFakeUser();
            var newPassword = "NewPass@123";

            _userRepoMock.Setup(r => r.GetByIdAsync(UserId)).ReturnsAsync(user);

            _passwordHasherMock
                .Setup(p => p.HashPassword(newPassword))
                .Returns("reset_hashed_password");

            _userRepoMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

            // Act
            var result = await _service.ResetPasswordAsync(UserId, newPassword);

            // Assert
            result.Should().BeTrue();
            user.PasswordHash.Should().Be("reset_hashed_password"); // كلمة المرور تغيّرت

            _passwordHasherMock.Verify(p => p.HashPassword(newPassword), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: مستخدم غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task ResetPasswordAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.ResetPasswordAsync(999, "NewPass@123");

            // Assert
            result.Should().BeFalse();

            _passwordHasherMock.Verify(p => p.HashPassword(It.IsAny<string>()), Times.Never);
        }

        // ============================================
        // AssignRoleAsync Tests
        // ============================================

        // الاختبار الأول: دور صحيح — يجب أن يُعيَّن ويرجع true
        [Fact]
        [Trait("Category", "Service")]
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
            user.Role.Should().Be(UserRole.TenantAdmin); // الدور تغيّر

            _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        // الاختبار الثاني: دور غير صحيح — يجب أن يرمي استثناء
        [Fact]
        [Trait("Category", "Service")]
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

        // الاختبار الثالث: مستخدم غير موجود — يجب أن يرجع false
        [Fact]
        [Trait("Category", "Service")]
        public async Task AssignRoleAsync_WhenUserNotFound_ShouldReturnFalse()
        {
            // Arrange
            _userRepoMock
                .Setup(r => r.GetByIdAsync(999))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.AssignRoleAsync(999, "TenantAdmin");

            // Assert
            result.Should().BeFalse();

            _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        }
    }
}