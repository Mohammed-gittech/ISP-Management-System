using System.Linq.Expressions;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ISP.Tests.Helpers
{
    public static class ReportTestHelper
    {
        // ============================================
        // Method 1: Create In-Memory Context
        // ============================================

        /// <summary>
        /// إنشاء ApplicationDbContext مع In-Memory Database
        /// </summary>
        public static ApplicationDbContext CreateInMemoryContext(int tenantId = 1)
        {
            // 1. إنشاء In-Memory Database بـ GUID عشوائي
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            // 2. إنشاء TestCurrentTenantService
            var tenantService = new TestCurrentTenantService();
            tenantService.SetTenant(tenantId);

            // 3. إنشاء Context
            var context = new ApplicationDbContext(options, tenantService);

            return context;
        }

        // ============================================
        // Method 2: Seed Report Data
        // ============================================

        /// <summary>
        /// ملء Database ببيانات وهمية للـ Reports
        /// </summary>
        public static void SeedReportData(ApplicationDbContext context)
        {
            // 1. Tenants (2)

            var tenant1 = new Tenant
            {
                Id = 1,
                Name = "FastNet ISP",
                Subdomain = "fastnet",
                MaxSubscribers = 1000,
                SubscriptionPlan = TenantPlan.Pro,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            };

            var tenant2 = new Tenant
            {
                Id = 2,
                Name = "SpeedNet ISP",
                Subdomain = "speednet",
                MaxSubscribers = 500,
                SubscriptionPlan = TenantPlan.Basic,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            };

            context.Tenants.AddRange(tenant1, tenant2);

            // ============================================
            // 2. Plans (3 for Tenant 1, 1 for Tenant 2)
            // ============================================

            var planGold = new Plan
            {
                Id = 1,
                TenantId = 1,
                Name = "Gold 100 Mbps",
                Speed = 100,
                Price = 20000,
                DurationDays = 30,
                IsActive = true
            };
            var planSilver = new Plan
            {
                Id = 2,
                TenantId = 1,
                Name = "Silver 50 Mbps",
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
                IsActive = true
            };
            var planBronze = new Plan
            {
                Id = 3,
                TenantId = 1,
                Name = "Bronze 25 Mbps",
                Speed = 25,
                Price = 10000,
                DurationDays = 30,
                IsActive = true
            };

            // Tenant 2 Plans
            var planBasic = new Plan
            {
                Id = 4,
                TenantId = 2,
                Name = "Basic 20 Mbps",
                Speed = 20,
                Price = 8000,
                DurationDays = 30,
                IsActive = true
            };

            context.Plans.AddRange(planGold, planSilver, planBronze, planBasic);

            // ============================================
            // 3. Subscribers (Tenant 1: 5, Tenant 2: 1)
            // ============================================

            // Tenant 1 Subscribers
            var sub1 = new Subscriber
            {
                Id = 1,
                TenantId = 1,
                FullName = "أحمد محمد",
                PhoneNumber = "07801111111",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow.AddDays(-60)
            };

            var sub2 = new Subscriber
            {
                Id = 2,
                TenantId = 1,
                FullName = "سارة علي",
                PhoneNumber = "07802222222",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow.AddDays(-45)
            };
            var sub3 = new Subscriber
            {
                Id = 3,
                TenantId = 1,
                FullName = "محمد حسن",
                PhoneNumber = "07803333333",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow.AddDays(-30)
            };

            var sub4 = new Subscriber
            {
                Id = 4,
                TenantId = 1,
                FullName = "فاطمة خالد",
                PhoneNumber = "07804444444",
                Status = SubscriberStatus.Inactive,
                RegistrationDate = DateTime.UtcNow.AddDays(-90)
            };

            var sub5 = new Subscriber
            {
                Id = 5,
                TenantId = 1,
                FullName = "علي حسين",
                PhoneNumber = "07805555555",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow.AddDays(-15), // جديد (شهر حالي)
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow.AddDays(-2) // في الشهر الحالي للـ Churned Test // في الشهر الحالي للـ Churned Test
            };
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // Subscriber جديد في الشهر الحالي (للـ NewSubscribers Test)
            var sub6 = new Subscriber
            {
                Id = 6,
                TenantId = 1,
                FullName = "زينب أحمد",
                PhoneNumber = "07806666666",
                Status = SubscriberStatus.Active,
                RegistrationDate = startOfMonth.AddDays(4) // في الشهر الحالي ✅
            };

            // Tenant 2 Subscribers (Multi-Tenancy Test)
            var sub7 = new Subscriber
            {
                Id = 7,
                TenantId = 2,
                FullName = "خالد كريم",
                PhoneNumber = "07806666666",
                Status = SubscriberStatus.Active,
                RegistrationDate = DateTime.UtcNow.AddDays(-20)
            };

            context.Subscribers.AddRange(sub1, sub2, sub3, sub4, sub5, sub6, sub7);

            // ============================================
            // 4. Subscriptions (Tenant 1: 12)
            // ============================================

            // Active Subscriptions (6)
            context.Subscriptions.Add(new Subscription
            {
                Id = 1,
                TenantId = 1,
                SubscriberId = 1,
                PlanId = 1, // Gold
                StartDate = startOfMonth.AddDays(-30),
                EndDate = startOfMonth.AddDays(30),
                Status = SubscriptionStatus.Active,
                CreatedAt = startOfMonth.AddDays(-30)
            });

            context.Subscriptions.Add(new Subscription
            {
                Id = 2,
                TenantId = 1,
                SubscriberId = 2,
                PlanId = 1, // Gold
                StartDate = startOfMonth.AddDays(-20),
                EndDate = startOfMonth.AddDays(40),
                Status = SubscriptionStatus.Active,
                CreatedAt = startOfMonth.AddDays(-20)
            });

            context.Subscriptions.Add(new Subscription
            {
                Id = 3,
                TenantId = 1,
                SubscriberId = 3,
                PlanId = 2, // Silver
                StartDate = startOfMonth.AddDays(-15),
                EndDate = startOfMonth.AddDays(45),
                Status = SubscriptionStatus.Active,
                CreatedAt = startOfMonth.AddDays(-15)
            });

            // Expiring Subscriptions (2)
            context.Subscriptions.Add(new Subscription
            {
                Id = 4,
                TenantId = 1,
                SubscriberId = 1,
                PlanId = 3, // Bronze
                StartDate = startOfMonth.AddDays(-60),
                EndDate = now.AddDays(3), // ينتهي بعد 3 أيام
                Status = SubscriptionStatus.Expiring,
                CreatedAt = startOfMonth.AddDays(-60)
            });

            context.Subscriptions.Add(new Subscription
            {
                Id = 5,
                TenantId = 1,
                SubscriberId = 2,
                PlanId = 2, // Silver
                StartDate = startOfMonth.AddDays(-90),
                EndDate = now.AddDays(1), // ينتهي غداً
                Status = SubscriptionStatus.Expiring,
                CreatedAt = startOfMonth.AddDays(-90)
            });

            // Expired Subscriptions (2)
            context.Subscriptions.Add(new Subscription
            {
                Id = 6,
                TenantId = 1,
                SubscriberId = 4,
                PlanId = 3, // Bronze
                StartDate = startOfMonth.AddDays(-120),
                EndDate = now.AddDays(-5), // منتهي منذ 5 أيام
                Status = SubscriptionStatus.Expired,
                CreatedAt = startOfMonth.AddDays(-120)
            });

            // New Subscription (Current Month)
            context.Subscriptions.Add(new Subscription
            {
                Id = 7,
                TenantId = 1,
                SubscriberId = 3,
                PlanId = 1, // Gold
                StartDate = startOfMonth.AddDays(10), // جديد في الشهر الحالي
                EndDate = startOfMonth.AddDays(70),
                Status = SubscriptionStatus.Active,
                CreatedAt = startOfMonth.AddDays(4) // في الشهر الحالي، قبل التاريخ الحالي
            });

            // Tenant 2 Subscription (Multi-Tenancy)
            context.Subscriptions.Add(new Subscription
            {
                Id = 8,
                TenantId = 2,
                SubscriberId = 6,
                PlanId = 4, // Basic
                StartDate = startOfMonth.AddDays(-10),
                EndDate = startOfMonth.AddDays(50),
                Status = SubscriptionStatus.Active,
                CreatedAt = startOfMonth.AddDays(-10)
            });

            // ============================================
            // 5. Invoices & Payments (Tenant 1)
            // ============================================

            // Paid Invoice 1 (أحمد - Gold)
            var invoice1 = new Invoice
            {
                Id = 1,
                TenantId = 1,
                SubscriberId = 1,
                InvoiceNumber = "INV-2026-0001",
                Subtotal = 20000,
                Tax = 0,
                Discount = 0,
                Total = 20000,
                Status = "Paid",
                IssuedDate = startOfMonth.AddDays(1), // في الشهر الحالي
                PaidDate = startOfMonth.AddDays(3)
            };

            var payment1 = new Payment
            {
                Id = 1,
                TenantId = 1,
                SubscriberId = 1,
                SubscriptionId = 1,
                InvoiceId = 1,
                Amount = 20000,
                PaymentMethod = "Cash",
                Status = "Completed",
                PaidAt = startOfMonth.AddDays(3), // مطابق لـ PaidDate
                CreatedAt = startOfMonth.AddDays(3)
            };

            invoice1.PaymentId = 1;

            // Paid Invoice 2 (سارة - Gold)
            var invoice2 = new Invoice
            {
                Id = 2,
                TenantId = 1,
                SubscriberId = 2,
                InvoiceNumber = "INV-2026-0002",
                Subtotal = 20000,
                Total = 20000,
                Status = "Paid",
                IssuedDate = startOfMonth.AddDays(2), // في الشهر الحالي
                PaidDate = startOfMonth.AddDays(4)
            };

            var payment2 = new Payment
            {
                Id = 2,
                TenantId = 1,
                SubscriberId = 2,
                SubscriptionId = 2,
                InvoiceId = 2,
                Amount = 20000,
                PaymentMethod = "Cash",
                Status = "Completed",
                PaidAt = startOfMonth.AddDays(4)
            };

            invoice2.PaymentId = 2;

            // Paid Invoice 3 (محمد - Silver)
            var invoice3 = new Invoice
            {
                Id = 3,
                TenantId = 1,
                SubscriberId = 3,
                InvoiceNumber = "INV-2026-0003",
                Subtotal = 15000,
                Total = 15000,
                Status = "Paid",
                IssuedDate = startOfMonth.AddDays(3), // في الشهر الحالي
                PaidDate = startOfMonth.AddDays(5)
            };

            var payment3 = new Payment
            {
                Id = 3,
                TenantId = 1,
                SubscriberId = 3,
                SubscriptionId = 3,
                InvoiceId = 3,
                Amount = 15000,
                PaymentMethod = "Online",
                Status = "Completed",
                PaidAt = startOfMonth.AddDays(5)
            };

            invoice3.PaymentId = 3;

            // Unpaid Invoice 4 (أحمد - Bronze)
            var invoice4 = new Invoice
            {
                Id = 4,
                TenantId = 1,
                SubscriberId = 1,
                InvoiceNumber = "INV-2026-0004",
                Subtotal = 10000,
                Total = 10000,
                Status = "Unpaid",
                IssuedDate = startOfMonth.AddDays(20),
                DueDate = startOfMonth.AddDays(27)
            };

            // Unpaid Invoice 5 (سارة - Silver)
            var invoice5 = new Invoice
            {
                Id = 5,
                TenantId = 1,
                SubscriberId = 2,
                InvoiceNumber = "INV-2026-0005",
                Subtotal = 15000,
                Total = 15000,
                Status = "Unpaid",
                IssuedDate = startOfMonth.AddDays(25),
                DueDate = startOfMonth.AddDays(30).AddDays(-1) // آخر يوم في الشهر
            };

            context.Invoices.AddRange(invoice1, invoice2, invoice3, invoice4, invoice5);
            context.Payments.AddRange(payment1, payment2, payment3);

            // Save All
            context.SaveChanges();

            // Clear ChangeTracker
            context.ChangeTracker.Clear();


            // ============================================
            // شرح البيانات الوهمية:
            // ============================================
            //
            // Tenant 1 (TenantId = 1):
            // 
            // Subscribers: 5
            //   - Active: 3 (أحمد، سارة، محمد)
            //   - Inactive: 1 (فاطمة)
            //   - Deleted: 1 (علي)
            //
            // Plans: 3
            //   - Gold 100 Mbps: 20,000
            //   - Silver 50 Mbps: 15,000
            //   - Bronze 25 Mbps: 10,000
            //
            // Subscriptions: 7
            //   - Active: 4
            //   - Expiring: 2 (3 أيام، 1 يوم)
            //   - Expired: 1
            //
            // Invoices: 5
            //   - Paid: 3 (Total: 55,000)
            //     * Invoice 1: 20,000 (أحمد - Gold) + Payment
            //     * Invoice 2: 20,000 (سارة - Gold) + Payment
            //     * Invoice 3: 15,000 (محمد - Silver) + Payment
            //   - Unpaid: 2 (Total: 25,000)
            //     * Invoice 4: 10,000 (أحمد - Bronze)
            //     * Invoice 5: 15,000 (سارة - Silver)
            //
            // Payments: 3 (مرتبطة بالفواتير المدفوعة)
            //
            // Tenant 2: بيانات منفصلة للـ Multi-Tenancy Test
            //
            // ============================================
        }
        public static (DateTime start, DateTime end) GetCurrentMonth()
        {
            var now = DateTime.UtcNow;
            var startDate = new DateTime(now.Year, now.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            return (startDate, endDate);
        }
    }
    // ============================================
    // TestCurrentTenantService (من Integration Tests)
    // ============================================

    public class TestCurrentTenantService : ICurrentTenantService
    {
        private int? _tenantId;
        private bool _isSuperAdmin;

        public int TenantId
        {
            get
            {
                if (_tenantId == null)
                    throw new InvalidOperationException("Tenant context not set.");
                return _tenantId.Value;
            }
        }

        public bool IsSuperAdmin => _isSuperAdmin;
        public int? UserId => null;
        public string? Username => null;
        public bool HasTenant => _tenantId != null;

        public void SetTenant(int tenantId)
        {
            _tenantId = tenantId;
            _isSuperAdmin = false;
        }

        public void SetSuperAdmin()
        {
            _isSuperAdmin = true;
            _tenantId = null;
        }
    }
}