using FluentAssertions;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Repositories;
using ISP.Tests.Helpers;

namespace ISP.Tests.Integration.Repositories
{
    public class TenantRepositoryTests
    {
        // ============================================
        // Helper Methods — بيانات وهمية للاختبارات
        // ============================================

        // ينشئ Tenant وهمي مباشرة في قاعدة البيانات
        // plan تتحكم في نوع الباقة
        private async Task<Tenant> CreateTenantAsync(
            ApplicationDbContext context,
            int id,
            string name,
            TenantPlan plan = TenantPlan.Free,
            bool isActive = true,
            bool isDeleted = false)
        {
            var tenant = new Tenant
            {
                Id = id,
                Name = name,
                ContactEmail = $"info@{name.ToLower()}.com",
                SubscriptionPlan = plan,
                MaxSubscribers = plan == TenantPlan.Free ? 50 : 500,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = isDeleted
            };

            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        }

        // ينشئ TenantSubscription وهمي مرتبط بـ Tenant
        // status تتحكم في حالة الاشتراك (Active, Pending, ...)
        private async Task<TenantSubscription> CreateSubscriptionAsync(
            ApplicationDbContext context,
            int tenantId,
            TenantSubscriptionStatus status = TenantSubscriptionStatus.Active,
            TenantPlan plan = TenantPlan.Free)
        {
            var subscription = new TenantSubscription
            {
                TenantId = tenantId,
                Plan = plan,
                Price = plan == TenantPlan.Free ? 0 : 29,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                Status = status
            };

            context.TenantSubscriptions.Add(subscription);
            await context.SaveChangesAsync();
            return subscription;
        }

        // ينشئ TenantPayment وهمي مرتبط بـ Tenant و Subscription
        private async Task<TenantPayment> CreatePaymentAsync(
            ApplicationDbContext context,
            int tenantId,
            int subscriptionId,
            decimal amount = 29)
        {
            var payment = new TenantPayment
            {
                TenantId = tenantId,
                TenantSubscriptionId = subscriptionId,
                Amount = amount,
                Currency = "USD",
                PaymentMethod = "Bank Transfer",
                PaymentGateway = "Manual",
                Status = "Completed",
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            context.TenantPayments.Add(payment);
            await context.SaveChangesAsync();
            return payment;
        }

        // ============================================
        // Multi-Tenancy Tests
        // ============================================

        // TEST 1: SuperAdmin يرى كل الـ Tenants
        [Fact]
        public async Task GetAllAsync_AsSuperAdmin_ShouldReturnAllTenants()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");
            await CreateTenantAsync(context, id: 3, name: "شركة الأمل");

            // Act
            var result = await repo.GetAllAsync();

            // Assert
            result.Should().HaveCount(3);
        }

        // TEST 2: SuperAdmin يجلب Tenant بالـ Id
        [Fact]
        public async Task GetByIdAsync_AsSuperAdmin_ShouldReturnCorrectTenant()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            // Act
            var result = await repo.GetByIdAsync(2);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("شركة الفجر");
        }

        // ============================================
        // Soft Delete Tests
        // ============================================

        // TEST 3: GetAllAsync لا يُرجع Tenant محذوف
        [Fact]
        public async Task GetAllAsync_ShouldNotReturnSoftDeletedTenant()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            // نضيف Tenant نشط وآخر محذوف
            await CreateTenantAsync(context, id: 1, name: "شركة النور", isDeleted: false);
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر", isDeleted: true);

            // Act
            var result = await repo.GetAllAsync();

            // Assert
            // يجب أن يظهر النشط فقط — المحذوف مخفي بالـ Global Filter
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("شركة النور");
        }

        // TEST 4: SoftDeleteAsync يُخفي الـ Tenant من الاستعلامات العادية
        [Fact]
        public async Task SoftDeleteAsync_ShouldHideTenantFromNormalQueries()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            var tenant = await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نتأكد أنه موجود قبل الحذف
            var beforeDelete = await repo.GetAllAsync();
            beforeDelete.Should().HaveCount(1);

            // Act
            await repo.SoftDeleteAsync(tenant);
            await context.SaveChangesAsync();

            // Assert
            // بعد Soft Delete لا يجب أن يظهر في GetAllAsync
            var afterDelete = await repo.GetAllAsync();
            afterDelete.Should().BeEmpty();

            // لكن في قاعدة البيانات IsDeleted = true
            tenant.IsDeleted.Should().BeTrue();
            tenant.DeletedAt.Should().NotBeNull();
        }

        // TEST 5: GetDeletedAsync يُرجع المحذوفين فقط
        [Fact]
        public async Task GetDeletedAsync_ShouldReturnOnlySoftDeletedTenants()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            // نشط ومحذوف في نفس قاعدة البيانات
            await CreateTenantAsync(context, id: 1, name: "شركة النور", isDeleted: false);
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر", isDeleted: true);

            // Act
            var result = await repo.GetDeletedAsync();

            // Assert
            // يجب أن يرجع المحذوف فقط
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("شركة الفجر");
            result.All(t => t.IsDeleted == true).Should().BeTrue();
        }

        // TEST 6: RestoreAsync يُعيد الـ Tenant للاستعلامات العادية
        [Fact]
        public async Task RestoreAsync_ShouldMakeTenantVisibleAgain()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<Tenant>(context, fakeSuperAdmin);

            // نضيف Tenant محذوف
            var tenant = await CreateTenantAsync(context, id: 1, name: "شركة النور", isDeleted: true);

            // نتأكد أنه لا يظهر في GetAllAsync
            var beforeRestore = await repo.GetAllAsync();
            beforeRestore.Should().BeEmpty();

            // Act
            await repo.RestoreAsync(tenant);
            await context.SaveChangesAsync();

            // Assert
            // بعد Restore يجب أن يظهر مجدداً
            var afterRestore = await repo.GetAllAsync();
            afterRestore.Should().HaveCount(1);
            tenant.IsDeleted.Should().BeFalse();
            tenant.DeletedAt.Should().BeNull();
        }

        // ============================================
        // Pending Renewals Tests
        // ============================================

        // TEST 7: يرجع الاشتراكات المعلقة فقط
        [Fact]
        public async Task GetAllAsync_PendingSubscriptions_ShouldReturnOnlyPending()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<TenantSubscription>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            await CreateTenantAsync(context, id: 2, name: "شركة الفجر");

            // نضيف اشتراك Active واشتراك Pending
            await CreateSubscriptionAsync(context, tenantId: 1, status: TenantSubscriptionStatus.Active);
            await CreateSubscriptionAsync(context, tenantId: 2, status: TenantSubscriptionStatus.Pending);

            // Act
            // نجلب الاشتراكات المعلقة فقط
            var result = await repo.GetAllAsync(s => s.Status == TenantSubscriptionStatus.Pending);

            // Assert
            result.Should().HaveCount(1);
            result.First().Status.Should().Be(TenantSubscriptionStatus.Pending);
        }

        // TEST 8: لا يوجد اشتراكات معلقة — يرجع قائمة فارغة
        [Fact]
        public async Task GetAllAsync_NoPendingSubscriptions_ShouldReturnEmpty()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<TenantSubscription>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف اشتراك Active فقط — لا يوجد Pending
            await CreateSubscriptionAsync(context, tenantId: 1, status: TenantSubscriptionStatus.Active);

            // Act
            var result = await repo.GetAllAsync(s => s.Status == TenantSubscriptionStatus.Pending);

            // Assert
            result.Should().BeEmpty();
        }

        // TEST 9: تأكيد الدفع يُغيّر حالة الاشتراك من Pending إلى Active
        [Fact]
        public async Task UpdateAsync_ConfirmPayment_ShouldChangeStatusToActive()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var repo = new GenericRepository<TenantSubscription>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");

            // نضيف اشتراك Pending ينتظر تأكيد الدفع
            var subscription = await CreateSubscriptionAsync(
                context,
                tenantId: 1,
                status: TenantSubscriptionStatus.Pending);

            // نتأكد أنه Pending قبل التحديث
            subscription.Status.Should().Be(TenantSubscriptionStatus.Pending);

            // Act
            // نحاكي ما يفعله ConfirmPaymentAsync
            subscription.Status = TenantSubscriptionStatus.Active;
            subscription.PaymentMethod = "Bank Transfer";
            subscription.LastPaymentDate = DateTime.UtcNow;

            await repo.UpdateAsync(subscription);
            await context.SaveChangesAsync();

            // Assert
            // نجلبه من الـ DB للتأكد أن التغيير حُفظ فعلاً
            var updated = await repo.GetByIdAsync(subscription.Id);
            updated.Should().NotBeNull();
            updated!.Status.Should().Be(TenantSubscriptionStatus.Active);
            updated.PaymentMethod.Should().Be("Bank Transfer");
            updated.LastPaymentDate.Should().NotBeNull();
        }

        // TEST 10: TenantPayment يُنشأ بعد تأكيد الدفع
        [Fact]
        public async Task AddAsync_TenantPayment_ShouldPersistPaymentRecord()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContextAsSuperAdmin();
            var fakeSuperAdmin = new FakeTenantService(isSuperAdmin: true);
            var paymentRepo = new GenericRepository<TenantPayment>(context, fakeSuperAdmin);

            await CreateTenantAsync(context, id: 1, name: "شركة النور");
            var subscription = await CreateSubscriptionAsync(context, tenantId: 1);

            // Act
            // نحاكي إنشاء TenantPayment عند تأكيد الدفع
            var payment = await CreatePaymentAsync(
                context,
                tenantId: 1,
                subscriptionId: subscription.Id,
                amount: 87); // 29 × 3 أشهر

            // Assert
            // نجلبه من الـ DB للتأكد أنه حُفظ
            var saved = await paymentRepo.GetByIdAsync(payment.Id);
            saved.Should().NotBeNull();
            saved!.Amount.Should().Be(87);
            saved.Status.Should().Be("Completed");
            saved.TenantId.Should().Be(1);
        }
    }
}