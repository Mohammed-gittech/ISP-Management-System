// ============================================
// SubscriptionRepositoryTests.cs
// Integration Tests for Subscription Repository
// ============================================
// Unlike Unit Tests, these tests use a real
// InMemory database to verify that:
// 1. Multi-Tenancy isolation works correctly
// 2. Soft Delete hides data from normal queries
// 3. GetDeleted returns only deleted records
// 4. Restore brings data back to normal queries
// ============================================

using FluentAssertions;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Migrations;
using ISP.Infrastructure.Repositories;
using ISP.Tests.Helpers;

namespace ISP.Tests.Integration.Repositories
{
    public class SubscriptionRepositoryTests
    {
        // ============================================
        // Helper Methods — بيانات وهمية للاختبارات
        // ============================================

        // وهمي في قاعدة البيانات Tenant ينشئ 
        private async Task<Tenant> CreateTenantAsync(ApplicationDbContext context, int id, string name)
        {
            var tenant = new Tenant { Id = id, Name = name };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
            return tenant;
        }

        // وهمي في قاعدة البيانات Plan ينشئ 
        private async Task<Plan> CreatePlanAsync(ApplicationDbContext context, int tenantId)
        {
            var plan = new Plan
            {
                TenantId = tenantId,
                Name = "50 Mbps Plan",
                Speed = 50,
                Price = 100,
                DurationDays = 30,
                IsActive = true,
            };

            context.Plans.Add(plan);
            await context.SaveChangesAsync();
            return plan;
        }

        // وهمي في قاعدة البيانات Subscriber ينشئ  
        private async Task<Subscriber> CreateSubscriberAsync(ApplicationDbContext context, int tenantId)
        {
            var subscriber = new Subscriber
            {
                TenantId = tenantId,
                FullName = "Ahmed Mohammed",
                PhoneNumber = "0501234567"
            };
            context.Subscribers.Add(subscriber);
            await context.SaveChangesAsync();
            return subscriber;
        }

        // وهمي في قاعدة البيانات Subscription ينشئ  
        private async Task<Subscription> CreateSubscriptionAsync(
            ApplicationDbContext context,
            int tenantId,
            int subscriberId,
            int planId,
            bool isDeleted = false)
        {
            var subscription = new Subscription
            {
                TenantId = tenantId,
                SubscriberId = subscriberId,
                PlanId = planId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                Status = SubscriptionStatus.Active,
                IsDeleted = isDeleted,
                DeletedAt = isDeleted ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow,
            };

            // IgnoreQueryFilters لأننا نريد إضافة البيانات مباشرة
            // بغض النظر عن Global Query Filter
            context.Subscriptions.Add(subscription);
            await context.SaveChangesAsync();
            return subscription;
        }


        // ============================================
        // Multi-Tenancy Tests
        // ============================================

        // TEST 1: يرى بياناته فقط Tenant كل
        [Fact]
        public async Task GetAllAsync_ShouldReturnOnlyCurrentTenantSubscriptions()
        {
            // Arrange
            // Tenant 1 لـ Context ننشئ 
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<Subscription>(context, fakeTenant);

            // Tenant 1 نضيف بيانات لـ
            var plan1 = await CreatePlanAsync(context, tenantId: 1);
            var subscriber1 = await CreateSubscriberAsync(context, tenantId: 1);
            await CreateSubscriptionAsync(context, tenantId: 1, subscriber1.Id, plan1.Id);

            // Tenant 2 نضيف بيانات لـ
            var plan2 = await CreatePlanAsync(context, tenantId: 2);
            var subscriber2 = await CreateSubscriberAsync(context, tenantId: 2);
            await CreateSubscriptionAsync(context, tenantId: 2, subscriber2.Id, plan2.Id);

            // Act 
            var result = await repo.GetAllAsync();

            // Assert 
            // Tenant 2 يجب أن يرى اشتراكه فقط — وليس اشتراك Tenant 1
            result.Should().HaveCount(1);
            result.All(s => s.TenantId == 1).Should().BeTrue();
        }

        // ============================================
        // Soft Delete Tests
        // ============================================

        // TEST 2: GetAllAsync  الاشتراك المحذوف لا يظهر في
        [Fact]
        public async Task GetAllAsync_ShouldNotReturnSoftDeletedSubscriptions()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<Subscription>(context, fakeTenant);

            var plan = await CreatePlanAsync(context, tenantId: 1);
            var subscriber = await CreateSubscriberAsync(context, tenantId: 1);

            // نضيف اشتراكاً نشطاً
            await CreateSubscriptionAsync(context, tenantId: 1, subscriber.Id, plan.Id, isDeleted: false);

            // نضيف اشتراكاً محذوفاً
            await CreateSubscriptionAsync(context, tenantId: 1, subscriber.Id, plan.Id, isDeleted: true);

            // Act
            var results = await repo.GetAllAsync();

            // Assert
            // يجب أن يظهر الاشتراك النشط فقط — المحذوف مخفي بالـ Global Filter
            results.Should().HaveCount(1);
            results.All(s => s.IsDeleted == false).Should().BeTrue();
        }

        // TEST 3: SoftDeleteAsync يُخفي الاشتراك
        [Fact]
        public async Task SoftDeleteAsync_ShouldHideSubscriptionFromNormalQueries()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<Subscription>(context, fakeTenant);

            var plan = await CreatePlanAsync(context, tenantId: 1);
            var subscriber = await CreateSubscriberAsync(context, tenantId: 1);
            var subscription = await CreateSubscriptionAsync(context, tenantId: 1, subscriber.Id, plan.Id);

            // نتأكد أنه موجود قبل الحذف
            var beforeDelete = await repo.GetAllAsync();
            beforeDelete.Should().HaveCount(1);

            // Act
            await repo.SoftDeleteAsync(subscription);
            await context.SaveChangesAsync();

            // Assert
            // GetAllAsync لا يجب أن يظهر في Soft Delete بعد
            var afterDelete = await repo.GetAllAsync();
            afterDelete.Should().BeEmpty();

            // في قاعدة البيانات IsDeleted = true لكن 
            subscription.IsDeleted.Should().BeTrue();
            subscription.DeletedAt.Should().NotBeNull();
        }

        // TEST 4: GetDeletedAsync يرجع المحذوفات فقط
        [Fact]
        public async Task GetDeletedAsync_ShouldReturnOnlySoftDeletedSubscriptions()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<Subscription>(context, fakeTenant);

            var plan = await CreatePlanAsync(context, tenantId: 1);
            var subscriber = await CreateSubscriberAsync(context, tenantId: 1);

            // نضيف اشتراكاً نشطاً واشتراكاً محذوفاً
            await CreateSubscriptionAsync(context, tenantId: 1, subscriber.Id, plan.Id, isDeleted: false);
            await CreateSubscriptionAsync(context, tenantId: 1, subscriber.Id, plan.Id, isDeleted: true);

            // Act
            var deletedResults = await repo.GetDeletedAsync();

            // Assert
            // يجب أن يرجع المحذوف فقط
            deletedResults.Should().HaveCount(1);
            deletedResults.All(s => s.IsDeleted == true).Should().BeTrue();
        }

        // TEST 5: RestoreAsync يُعيد الاشتراك للاستعلامات العادية
        [Fact]
        public async Task RestoreAsync_ShouldMakeSubscriptionVisibleAgain()
        {
            // Arrange
            using var context = TestDbContextFactory.CreateContext(tenantId: 1);
            var fakeTenant = new FakeTenantService(tenantId: 1);
            var repo = new GenericRepository<Subscription>(context, fakeTenant);

            var plan = await CreatePlanAsync(context, tenantId: 1);
            var subscriber = await CreateSubscriberAsync(context, tenantId: 1);

            // نضيف اشتراكاً محذوفاً
            var subscription = await CreateSubscriptionAsync(
                context, tenantId: 1, subscriber.Id, plan.Id, isDeleted: true);

            // GetAllAsync نتأكد أنه لا يظهر في  
            var beforeRestore = await repo.GetAllAsync();
            beforeRestore.Should().BeEmpty();

            // Act
            await repo.RestoreAsync(subscription);
            await context.SaveChangesAsync();

            // Assert
            // بعد Restore — يجب أن يظهر مجدداً
            var afterRestore = await repo.GetAllAsync();
            afterRestore.Should().HaveCount(1);
            subscription.IsDeleted.Should().BeFalse();
            subscription.DeletedAt.Should().BeNull();
        }
    }
}