// ============================================
// TestDbContextFactory.cs
// Factory for creating InMemory DbContext
// Used only in Integration Tests
// ============================================

using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISP.Tests.Helpers
{
    public static class TestDbContextFactory
    {
        // ============================================
        // CreateContext — ينشئ DbContext جديد نظيف
        // ============================================

        // كل استدعاء ينشئ قاعدة بيانات جديدة منفصلة
        // Guid.NewGuid() يضمن اسماً فريداً في كل مرة
        public static ApplicationDbContext CreateContext(int tenantId = 1)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // نمرر FakeTenantService بدل الحقيقي
            var fakeTenant = new FakeTenantService(tenantId);

            return new ApplicationDbContext(options, fakeTenant);
        }

        // ============================================
        // CreateContextAsSuperAdmin
        // ============================================

        // لاختبارات SuperAdmin الذي يرى كل البيانات
        public static ApplicationDbContext CreateContextAsSuperAdmin()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var fakeTenant = new FakeTenantService(isSuperAdmin: true);

            return new ApplicationDbContext(options, fakeTenant);
        }
    }
}