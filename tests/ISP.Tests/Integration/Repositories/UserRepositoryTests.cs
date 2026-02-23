// ============================================
// UserRepositoryTests.cs
// Integration Tests for User Repository
// ============================================
// بعكس Unit Tests، هذه الاختبارات تستخدم
// InMemory database حقيقية للتحقق من:
// 1. Multi-Tenancy: كل Tenant يرى مستخدميه فقط
// 2. SuperAdmin: يرى كل المستخدمين
// 3. Soft Delete: المحذوف لا يظهر في الاستعلامات العادية
// 4. Restore: يُعيد المستخدم للاستعلامات العادية
// 5. Permanent Delete: يحذف نهائياً من قاعدة البيانات
// ============================================

using FluentAssertions;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Repositories;
using ISP.Tests.Helpers;

namespace ISP.Tests.Integration.Repositories
{
    public class UserRepositoryTests
    {
        // ============================================
        // Helper Methods — بيانات وهمية للاختبارات
        // ============================================

        // ينشئ Tenant وهمي مباشرة في قاعدة البيانات
        private async Task<Tenant> CreateTenantAsync(ApplicationDbContext context, int id, string name)
        {
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        }

        // ينشئ User وهمي مباشرة في قاعدة البيانات
        // role تتحكم في الدور — isDeleted تتحكم في حالة الحذف
        private async Task<User> CreateUserAsync(
            ApplicationDbContext context,
            int? tenantId,
            string username,
            UserRole role = UserRole.Employee,
            bool isDeleted = false,
            bool isActive = true)
        {
            var user = new User
            {
                TenantId = tenantId,
                Username = username,
                Email = $"{username}@test.com",
                PasswordHash = "hashed_password",
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

        // ============================================
        // Multi-Tenancy Tests
        // ============================================

        // TEST 1: Tenant يرى مستخدميه فقط — لا يرى مستخدمي Tenant آخر
        [Fact]
        public async Task GetAllAsync_ShouldReturnOnlyCurrentTenantUsers()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            // نضيف مستخدم لـ Tenant 1 ومستخدم لـ Tenant 2
            await CreateUserAsync(context, tenantId: 1, username: "ahmed");
            await CreateUserAsync(context, tenantId: 2, username: "khalid");

            // Act
            var result = await repo.GetAllAsync();

            // Assert
            // Tenant 1 يرى مستخدمه فقط — khalid مخفي
            result.Should().HaveCount(1);
            result.First().Username.Should().Be("ahmed");
        }

        // TEST 2: Tenant لا يستطيع الوصول لمستخدم Tenant آخر بالـ Id
        [Fact]
        public async Task GetByIdAsync_ShouldNotReturnOtherTenantUser()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            await CreateUserAsync(context, tenantId: 1, username: "ahmed");
            var khalid = await CreateUserAsync(context, tenantId: 2, username: "khalid");

            // Act
            // Tenant 1 يحاول الوصول لمستخدم Tenant 2
            var result = await repo.GetByIdAsync(khalid.Id);

            // Assert
            // يجب أن يرجع null — Multi-Tenancy Filter يمنع الوصول
            result.Should().BeNull();
        }

        // TEST 3: SuperAdmin يرى مستخدمي كل التينانتس
        [Fact]
        public async Task GetAllAsync_AsSuperAdmin_ShouldReturnAllUsers()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<User>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            // نضيف مستخدمين لكلا الـ Tenants
            await CreateUserAsync(context, tenantId: 1, username: "ahmed");
            await CreateUserAsync(context, tenantId: 2, username: "khalid");

            // SuperAdmin بدون TenantId
            await CreateUserAsync(context, tenantId: null, username: "superadmin", role: UserRole.SuperAdmin);

            // Act
            var result = await repo.GetAllAsync();

            // Assert
            // SuperAdmin يرى الجميع — 3 مستخدمين
            result.Should().HaveCount(3);
        }

        // ============================================
        // Soft Delete Tests
        // ============================================

        // TEST 4: GetAllAsync لا يُرجع المستخدم المحذوف
        [Fact]
        public async Task GetAllAsync_ShouldNotReturnSoftDeletedUsers()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف مستخدم نشط ومستخدم محذوف
            await CreateUserAsync(context, tenantId: 1, username: "ahmed", isDeleted: false);
            await CreateUserAsync(context, tenantId: 1, username: "khalid", isDeleted: true);

            // Act
            var result = await repo.GetAllAsync();

            // Assert
            // يجب أن يظهر النشط فقط — المحذوف مخفي بالـ Global Filter
            result.Should().HaveCount(1);
            result.First().Username.Should().Be("ahmed");
        }

        // TEST 5: SoftDeleteAsync يُخفي المستخدم من الاستعلامات العادية
        [Fact]
        public async Task SoftDeleteAsync_ShouldHideUserFromNormalQueries()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            var user = await CreateUserAsync(context, tenantId: 1, username: "ahmed");

            // نتأكد أنه موجود قبل الحذف
            var beforeDelete = await repo.GetAllAsync();
            beforeDelete.Should().HaveCount(1);

            // Act
            await repo.SoftDeleteAsync(user);
            await context.SaveChangesAsync();

            // Assert
            // بعد Soft Delete لا يجب أن يظهر في GetAllAsync
            var afterDelete = await repo.GetAllAsync();
            afterDelete.Should().BeEmpty();

            // لكن في قاعدة البيانات IsDeleted = true
            user.IsDeleted.Should().BeTrue();
            user.DeletedAt.Should().NotBeNull();
        }

        // TEST 6: GetDeletedAsync يُرجع المحذوفين فقط
        [Fact]
        public async Task GetDeletedAsync_ShouldReturnOnlySoftDeletedUsers()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نشط ومحذوف في نفس قاعدة البيانات
            await CreateUserAsync(context, tenantId: 1, username: "ahmed", isDeleted: false);
            await CreateUserAsync(context, tenantId: 1, username: "khalid", isDeleted: true);

            // Act
            var result = await repo.GetDeletedAsync();

            // Assert
            // يجب أن يرجع المحذوف فقط
            result.Should().HaveCount(1);
            result.First().Username.Should().Be("khalid");
            result.All(u => u.IsDeleted == true).Should().BeTrue();
        }

        // ============================================
        // Restore Tests
        // ============================================

        // TEST 7: RestoreAsync يُعيد المستخدم للاستعلامات العادية
        [Fact]
        public async Task RestoreAsync_ShouldMakeUserVisibleAgain()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف مستخدم محذوف
            var user = await CreateUserAsync(context, tenantId: 1, username: "ahmed", isDeleted: true);

            // نتأكد أنه لا يظهر في GetAllAsync
            var beforeRestore = await repo.GetAllAsync();
            beforeRestore.Should().BeEmpty();

            // Act
            await repo.RestoreAsync(user);
            await context.SaveChangesAsync();

            // Assert
            // بعد Restore يجب أن يظهر مجدداً
            var afterRestore = await repo.GetAllAsync();
            afterRestore.Should().HaveCount(1);
            user.IsDeleted.Should().BeFalse();
            user.DeletedAt.Should().BeNull();
        }

        // ============================================
        // Permanent Delete Tests
        // ============================================

        // TEST 8: DeleteAsync يحذف المستخدم نهائياً من قاعدة البيانات
        [Fact]
        public async Task DeleteAsync_ShouldPermanentlyRemoveUser()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف مستخدم محذوف soft — الحذف النهائي يكون بعد Soft Delete
            var user = await CreateUserAsync(context, tenantId: 1, username: "ahmed", isDeleted: true);

            // Act
            await repo.DeleteAsync(user);
            await context.SaveChangesAsync();

            // Assert
            // حتى GetByIdIncludingDeletedAsync لا يجده — اختفى نهائياً
            var result = await repo.GetByIdIncludingDeletedAsync(user.Id);
            result.Should().BeNull();
        }

        // TEST 9: GetByIdIncludingDeletedAsync يجلب المحذوف soft
        [Fact]
        public async Task GetByIdIncludingDeletedAsync_ShouldReturnSoftDeletedUser()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<User>(context, fakeTenant);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف مستخدم محذوف
            var user = await CreateUserAsync(context, tenantId: 1, username: "ahmed", isDeleted: true);

            // نتأكد أن GetAllAsync لا يراه
            var normalQuery = await repo.GetAllAsync();
            normalQuery.Should().BeEmpty();

            // Act
            // GetByIdIncludingDeletedAsync يتجاهل الـ Global Filter
            var result = await repo.GetByIdIncludingDeletedAsync(user.Id);

            // Assert
            // يجب أن يجده رغم أنه محذوف
            result.Should().NotBeNull();
            result!.Username.Should().Be("ahmed");
            result.IsDeleted.Should().BeTrue();
        }
    }
}