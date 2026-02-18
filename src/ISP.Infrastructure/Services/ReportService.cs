using ISP.Application.DTOs.Reports;
using ISP.Application.Interfaces;
using ISP.Domain.Enums;
using ISP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    public class ReportService : IReportService
    {
        // ============================================
        // Dependencies (الاعتماديات)
        // ============================================

        private readonly ApplicationDbContext _context;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<ReportService> _logger;

        public ReportService(
            ApplicationDbContext context,
            ICurrentTenantService currentTenantService,
            ILogger<ReportService> logger
        )
        {
            _context = context;
            _currentTenant = currentTenantService;
            _logger = logger;
        }


        /// <summary>
        /// توليد تقرير الإيرادات
        /// </summary>
        public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // 1.1 جلب TenantId من الـ Middleware
            int tenantId = _currentTenant.TenantId;

            // 1.2 تسجيل بداية العملية (للـ Monitoring)
            _logger.LogInformation(
                "Generating Revenue Report for Tenant {TenantId}, Period: {StartDate} to {EndDate}",
                tenantId,
                startDate?.ToString("yyyy-MM-dd") ?? "All Time",
                endDate?.ToString("yyyy-MM-dd") ?? "Now"
            );

            // 1.3 تحديد الفترة الزمنية (Date Range)
            // لو المستخدم ما حدد، نستخدم القيم الافتراضية
            var effectiveStartDate = startDate ?? DateTime.MinValue;
            var effectiveEndDate = endDate ?? DateTime.UtcNow;

            // 2 جلب كل المدفوعات المكتملة للوكيل في الفترة المحددة
            var totalRevenue = await _context.Payments
                .Where(p => p.TenantId == tenantId)
                .Where(p => p.Status == PaymentStatus.Completed.ToString())
                .Where(p => p.CreatedAt >= effectiveStartDate && p.CreatedAt <= effectiveEndDate)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            _logger.LogInformation("Total Revenue calculated: {Amount}", totalRevenue);

            // 3 عدد الفواتير المدفوعة
            var paidInvoicesCount = await _context.Invoices
                .Where(i => i.TenantId == tenantId)
                .Where(i => i.Status == InvoiceStatus.Paid.ToString())
                .Where(i => i.IssuedDate >= effectiveStartDate && i.IssuedDate <= effectiveEndDate)
                .CountAsync();

            // 3 عدد الفواتير الغير مدفوعة
            var unpaidInvoicesCount = await _context.Invoices
                .Where(i => i.TenantId == tenantId)
                .Where(i => i.Status == InvoiceStatus.Unpaid.ToString() || i.Status == InvoiceStatus.Overdue.ToString())
                .Where(i => i.IssuedDate >= effectiveStartDate && i.IssuedDate <= effectiveEndDate)
                .CountAsync();

            // 4 إجمالي المبلغ الغير مدفوع
            var unpaidAmount = await _context.Invoices
                .Where(i => i.TenantId == tenantId)
                .Where(i => i.Status == InvoiceStatus.Unpaid.ToString() || i.Status == InvoiceStatus.Overdue.ToString())
                .Where(i => i.IssuedDate >= effectiveStartDate && i.IssuedDate <= effectiveEndDate)
                .SumAsync(i => (decimal?)i.Total) ?? 0;

            _logger.LogInformation(
                "Invoices: Paid={Paid}, Unpaid={Unpaid}, UnpaidAmount={Amount}",
                paidInvoicesCount, unpaidInvoicesCount, unpaidAmount
            );

            // ============================================
            // Step 4: Monthly Revenue (GroupBy حسب الشهر)
            // ============================================
            var monthlyRevenues = await _context.Invoices
            .Where(i => i.TenantId == tenantId)
                .Where(i => i.Status == InvoiceStatus.Paid.ToString())
                .Where(i => i.IssuedDate >= effectiveStartDate
                        && i.IssuedDate <= effectiveEndDate)
            .GroupBy(i => new { i.IssuedDate.Year, i.IssuedDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyRevenueDto
            {
                Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                Amount = g.Sum(i => i.Total),
                InvoicesCount = g.Count()
            })
            .ToListAsync();

            // ============================================
            // Step 5: Revenue By Plan (GroupBy حسب الباقة)
            // ============================================
            var revenueByPlan = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.CreatedAt >= effectiveStartDate
                    && s.CreatedAt <= effectiveEndDate)
                .GroupBy(s => s.Plan.Name)
                .Select(g => new PlanRevenueDto
                {
                    PlanName = g.Key,
                    SubscribersCount = g.Count(),
                    Revenue = g.Sum(s => s.Plan.Price),
                    Percentage = 0
                })
                .ToListAsync();

            // حساب النسب المئوية
            var totalSubscribers = revenueByPlan.Sum(p => p.SubscribersCount);
            if (totalSubscribers > 0)
            {
                foreach (var plan in revenueByPlan)
                {
                    plan.Percentage = Math.Round((decimal)plan.SubscribersCount / totalSubscribers * 100, 2);
                }
            }

            return new RevenueReportDto
            {
                TotalRevenue = totalRevenue,
                PaidInvoicesCount = paidInvoicesCount,
                UnpaidInvoicesCount = unpaidInvoicesCount,
                UnpaidAmount = unpaidAmount,
                MonthlyRevenues = monthlyRevenues,
                RevenueByPlan = revenueByPlan
            };
        }

        /// <summary>
        /// توليد تقرير نمو المشتركين
        /// </summary>
        public async Task<GrowthReportDto> GetGrowthReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            // Step 1: Setup
            int tenantId = _currentTenant.TenantId;

            _logger.LogInformation(
                "Generating Growth Report for Tenant {TenantId}, Period: {StartDate} to {EndDate}",
                tenantId,
                startDate?.ToString("yyyy-MM-dd") ?? "All Time",
                endDate?.ToString("yyyy-MM-dd") ?? "Now"
            );

            var effectiveStartDate = startDate ?? DateTime.MinValue;
            var effectiveEndDate = endDate ?? DateTime.UtcNow;

            // Step 2: Total Active Subscribers (المشتركين النشطين)
            var totalActiveSubscribers = await _context.Subscribers
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.Status == SubscriberStatus.Active)
                .CountAsync();

            // Step 3: Total All Subscribers (كل المشتركين)
            var totalAllSubscribers = await _context.Subscribers
                .Where(s => s.TenantId == tenantId)
                .CountAsync();

            // Step 4: New Subscribers (المشتركين الجدد)
            var newSubscribers = await _context.Subscribers
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.RegistrationDate >= effectiveStartDate
                    && s.RegistrationDate <= effectiveEndDate)
                .CountAsync();

            // Step 5: Churned Subscribers (الملغيين)
            var churnedSubscribers = await _context.Subscribers
                .IgnoreQueryFilters() // ✅ نريد نشوف المحذوفين
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.IsDeleted)
                .Where(s => s.DeletedAt >= effectiveStartDate
                    && s.DeletedAt <= effectiveEndDate)
                .CountAsync();

            // Step 6: Calculations (الحسابات)

            // Net Growth = New - Churned
            var netGrowth = newSubscribers - churnedSubscribers;

            // Total at start of period (للحساب النسب المئوية)
            // ✅ الحسبة الصحيحة: نجيب من Database مباشرة
            var totalAtStart = await _context.Subscribers
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.RegistrationDate < effectiveStartDate)
                .Where(s => !s.IsDeleted || s.DeletedAt >= effectiveStartDate)
                .CountAsync();

            // Growth Rate
            decimal growthRate = 0;
            if (totalAtStart > 0)
                growthRate = Math.Round((decimal)netGrowth / totalAtStart * 100, 2);

            // Churn Rate
            decimal churnRate = 0;
            if (totalAtStart > 0)
                churnRate = Math.Round((decimal)churnedSubscribers / totalAtStart * 100, 2);

            _logger.LogInformation(
                "Growth Stats - Active: {Active}, New: {New}, Churned: {Churned}, Growth Rate: {Rate}%",
                totalActiveSubscribers, newSubscribers, churnedSubscribers, growthRate
            );

            // Step 7: Monthly Trend (الترند الشهري)

            // المرحلة 1: New Subscribers شهرياً

            // نجمع البيانات شهر بشهر
            var monthlyTrend = await _context.Subscribers
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.RegistrationDate >= effectiveStartDate
                    && s.RegistrationDate <= effectiveEndDate)
                .GroupBy(s => new { s.RegistrationDate.Year, s.RegistrationDate.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyGrowthDto
                {
                    Month = $"{g.Key.Year}-{g.Key.Month}",
                    NewCount = g.Count(),
                    ActiveCount = 0,
                    ChurnedCount = 0,
                    NetGrowth = 0
                })
                .ToListAsync();

            // المرحلة 2: Churned Subscribers شهرياً
            // نجمع المشتركين المحذوفين حسب شهر الحذف
            var monthlyChurn = await _context.Subscribers
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.IsDeleted)
                .Where(s => s.DeletedAt >= effectiveStartDate
                    && s.DeletedAt <= effectiveEndDate)
                .GroupBy(s => new
                {
                    Year = s.DeletedAt!.Value.Year, // ! لأن DeletedAt nullable
                    Month = s.DeletedAt!.Value.Month
                })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Month = $"{g.Key.Year}-{g.Key.Month}",
                    Count = g.Count()
                })
                .ToListAsync();

            // المرحلة 3: ربط البيانات والحسابات
            // 3.1: نربط ChurnedCount من monthlyChurn إلى monthlyTrend
            foreach (var month in monthlyTrend)
            {
                // نبحث عن الشهر المطابق في monthlyChurn
                var churnData = monthlyChurn.FirstOrDefault(c => c.Month == month.Month);

                // لو موجود، نحط العدد، لو لا نحط 0
                month.ChurnedCount = churnData?.Count ?? 0;

                // نحسب NetGrowth
                month.NetGrowth = month.NewCount - month.ChurnedCount;
            }

            // 3.2: نحسب ActiveCount بالتراكم
            // أولاً: نحتاج نعرف العدد قبل أول شهر
            int runningCount = totalAtStart;

            // Loop على كل شهر ونحسب ActiveCount
            foreach (var month in monthlyTrend)
            {
                // نضيف NetGrowth للعدد الجاري
                runningCount += month.NetGrowth;

                // النتيجة = ActiveCount لهذا الشهر
                month.ActiveCount = runningCount;
            }

            _logger.LogInformation(
                "Monthly trend calculated - {Count} months",
                monthlyTrend.Count
            );

            // Step 8: Build & Return Report
            return new GrowthReportDto
            {
                TotalActiveSubscribers = totalActiveSubscribers,
                TotalAllSubscribers = totalAllSubscribers,
                NewSubscribers = newSubscribers,
                ChurnedSubscribers = churnedSubscribers,
                NetGrowth = netGrowth,
                GrowthRate = growthRate,
                ChurnRate = churnRate,
                MonthlyTrend = monthlyTrend
            };
        }

        /// <summary>
        /// توليد تقرير شعبية الباقات
        /// </summary>
        public async Task<PlanPopularityReportDto> GetPlanPopularityReportAsync(DateTime? startDate = null, DateTime? endDate = null, int? top = null)
        {
            // Step 1: Setup (الإعداد)
            int tenantId = _currentTenant.TenantId;

            _logger.LogInformation(
                "Generating Plan Popularity Report for Tenant {TenantId}, Period: {StartDate} to {EndDate}, Top: {Top}",
                tenantId,
                startDate?.ToString("yyyy-MM-dd") ?? "All Time",
                endDate?.ToString("yyyy-MM-dd") ?? "Now",
                top?.ToString() ?? "All"
            );

            var effectiveStartDate = startDate ?? DateTime.MinValue;
            var effectiveEndDate = endDate ?? DateTime.UtcNow;

            // Step 2: Query - GroupBy Subscriptions
            var plansData = await _context.Subscriptions
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.Status == SubscriptionStatus.Active
                        || s.Status == SubscriptionStatus.Expiring)
                .Where(s => s.CreatedAt >= effectiveStartDate
                    && s.CreatedAt <= effectiveEndDate)
                .GroupBy(s => s.PlanId)
                .Select(g => new
                {
                    PlanId = g.Key,
                    PlanName = g.First().Plan.Name,
                    Speed = g.First().Plan.Speed,
                    Price = g.First().Plan.Price,
                    SubscribersCount = g.Count()
                })
                .OrderByDescending(x => x.SubscribersCount)
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} plans with active subscribers",
                plansData.Count
            );

            // Step 3: Calculations (الحسابات)

            // 3.1: حساب إجمالي المشتركين
            int totalSubscribers = plansData.Sum(p => p.SubscribersCount);

            // 3.2: تحويل لـ PlanPopularityDto + حساب كل شيء
            var plans = new List<PlanPopularityDto>();
            int rank = 1;

            foreach (var plan in plansData)
            {
                // حساب النسبة المئوية
                decimal percentage = 0;
                if (totalSubscribers > 0)
                {
                    percentage = Math.Round((decimal)plan.SubscribersCount / totalSubscribers * 100, 2);
                }

                // حساب الإيرادات
                decimal monthlyRevenue = plan.SubscribersCount * plan.Price;
                decimal annualRevenue = monthlyRevenue * 12;

                // إنشاء DTO
                plans.Add(new PlanPopularityDto
                {
                    PlanId = plan.PlanId,
                    planName = plan.PlanName,
                    Speed = plan.Speed,
                    Price = plan.Price,
                    SubscribersCount = plan.SubscribersCount,
                    Percentage = percentage,
                    MonthlyRevenue = monthlyRevenue,
                    AnnualRevenue = annualRevenue,
                    Rank = rank++
                });
            }
            _logger.LogInformation(
                "Calculated stats for {Count} plans, Total Subscribers: {Total}",
                plans.Count,
                totalSubscribers
            );

            if (top.HasValue && top.Value > 0)
            {
                plans = plans.Take(top.Value).ToList();

                _logger.LogInformation(
                    "Applied Top filter: {Top}, Remaining plans: {Count}",
                    top.Value,
                    plans.Count
                );
            }

            // Step 5: Calculate Totals (حساب الإجماليات)
            int totalPlans = plans.Count;
            decimal totalMonthlyRevenue = plans.Sum(p => p.MonthlyRevenue);
            decimal totalAnnualRevenue = totalMonthlyRevenue * 12;

            _logger.LogInformation(
                "Report ready - Plans: {Plans}, Total Subs: {Subs}, Revenue: {Revenue}",
                totalPlans,
                totalSubscribers,
                totalMonthlyRevenue
            );

            // Step 6: Build & Return Report
            return new PlanPopularityReportDto
            {
                TotalSubscribers = totalSubscribers,
                TotalPlans = totalPlans,
                TotalMonthlyRevenue = totalMonthlyRevenue,
                TotalAnnualRevenue = totalAnnualRevenue,
                Plans = plans
            };
        }

        /// <summary>
        /// توليد تقرير الاشتراكات المنتهية قريباً
        /// </summary>
        public async Task<ExpiringSoonReportDto> GetExpiringSoonReportAsync(int? days = 7)
        {
            // Step 1: Setup (الإعداد)
            int tenantId = _currentTenant.TenantId;
            int effectiveDays = days ?? 7;

            _logger.LogInformation(
                "Generating Expiring Soon Report for Tenant {TenantId}, Days: {Days}",
                tenantId,
                effectiveDays
            );

            // Step 2: Query - Get Subscriptions
            var allSubscriptions = await _context.Subscriptions
                .Include(s => s.Subscriber)
                .Include(s => s.Plan)
                .Where(s => s.TenantId == tenantId)
                .Where(s => s.Status == SubscriptionStatus.Expiring
                        || s.Status == SubscriptionStatus.Expired)
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} subscriptions with Expiring/Expired status",
                allSubscriptions.Count
            );

            // Step 3: In-Memory Processing ⭐
            var subscriptions = new List<ExpiringSubscriptionDto>();

            foreach (var sub in allSubscriptions)
            {
                // 3.1: حساب DaysRemaining
                int daysRemaining = (sub.EndDate - DateTime.UtcNow).Days;

                // 3.2: فلترة حسب effectiveDays
                if (daysRemaining > effectiveDays && sub.Status != SubscriptionStatus.Expired)
                    continue; // تخطى هذا الاشتراك

                // 3.3: حساب Priority
                string priority = daysRemaining switch
                {
                    <= 1 => "High",
                    <= 3 => "Medium",
                    <= 7 => "Low",
                    _ => "Nono"
                };

                // 3.4: حساب Status String
                string status = sub.Status.ToString();

                subscriptions.Add(new ExpiringSubscriptionDto
                {
                    SubscriptionId = sub.Id,
                    SubscriberName = sub.Subscriber.FullName,
                    SubscriberPhone = sub.Subscriber.PhoneNumber,
                    PlanName = sub.Plan.Name,
                    Price = sub.Plan.Price,
                    EndDate = sub.EndDate,
                    DaysRemaining = daysRemaining,
                    Status = status,
                    Priority = priority
                });
            }

            _logger.LogInformation(
                "Processed {Count} subscriptions after filtering",
                subscriptions.Count
            );

            // Step 4: OrderBy DaysRemaining (الأقرب أولاً)
            subscriptions = subscriptions.OrderBy(s => s.DaysRemaining).ToList();

            // Step 5: Calculate Statistics (الإحصائيات)

            // 5.1: ExpiringSoon (خلال effectiveDays)
            int expiringSoon = subscriptions.Count(s => s.DaysRemaining >= 0 && s.DaysRemaining <= effectiveDays);

            // 5.2: ExpiringIn1Day
            int expiringIn1Day = subscriptions.Count(s => s.DaysRemaining <= 1 && s.DaysRemaining >= 0);

            // 5.3: ExpiringIn3Days
            int expiringIn3Days = subscriptions.Count(s => s.DaysRemaining <= 3 && s.DaysRemaining >= 0);

            // 5.4: ExpiringIn7Days
            int expiringIn7Days = subscriptions.Count(s => s.DaysRemaining <= 7 && s.DaysRemaining >= 0);

            // 5.5: AlreadyExpired (منتهي بالفعل)
            int alreadyExpired = subscriptions.Count(s => s.DaysRemaining < 0);

            // 5.6: PotentialRevenueLoss (الخسارة المحتملة)
            decimal potentialRevenueLoss = subscriptions.Where(s => s.DaysRemaining >= 0).Sum(s => s.Price);


            _logger.LogInformation(
                "Statistics - ExpiringSoon: {ExpiringSoon}, In1Day: {In1Day}, In3Days: {In3Days}, Loss: {Loss}",
                expiringSoon,
                expiringIn1Day,
                expiringIn3Days,
                potentialRevenueLoss
            );

            // Step 6: Build & Return Report
            return new ExpiringSoonReportDto
            {
                ExpiringSoon = expiringSoon,
                ExpiringIn1Day = expiringIn1Day,
                ExpiringIn3Days = expiringIn3Days,
                ExpiringIn7Days = expiringIn7Days,
                AlreadyExpired = alreadyExpired,
                Subscriptions = subscriptions
            };
        }


        /// <summary>
        /// توليد ملخص Dashboard - نظرة شاملة سريعة
        /// </summary>
        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            // Step 1: Setup (الإعداد)
            int tenantId = _currentTenant.TenantId;

            // حساب الشهر الحالي
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            _logger.LogInformation(
                "Generating Dashboard Summary for Tenant {TenantId}, Period: {StartDate} to {EndDate}",
                tenantId,
                startOfMonth.ToString("yyyy-MM-dd"),
                endOfMonth.ToString("yyyy-MM-dd")
            );

            // Step 2: Call All Reports (Parallel) ⚡

            // Todo DbContextFactory + Parallel Reports
            // 2.1: إنشاء Tasks (بدون await)
            // var revenueTask = GetRevenueReportAsync(startOfMonth, endOfMonth);
            // var growthTask = GetGrowthReportAsync(startOfMonth, endOfMonth);
            // var expiringTask = GetExpiringSoonReportAsync(7); // 7 أيام
            // var popularityTask = GetPlanPopularityReportAsync(startOfMonth, endOfMonth, null);


            // 2.2: ننتظر الكل يخلصون (Task.WhenAll)
            // await Task.WhenAll(revenueTask, growthTask, expiringTask, popularityTask);

            // 2.3: الحصول على النتائج
            // var revenueReport = revenueTask.Result;
            // var growthReport = growthTask.Result;
            // var expiringReport = expiringTask.Result;
            // var popularityReport = popularityTask.Result;

            var revenueReport = await GetRevenueReportAsync(startOfMonth, endOfMonth);
            var growthReport = await GetGrowthReportAsync(startOfMonth, endOfMonth);
            var expiringReport = await GetExpiringSoonReportAsync(7);
            var popularityReport = await GetPlanPopularityReportAsync(startOfMonth, endOfMonth, null);

            _logger.LogInformation(
                            "All reports fetched successfully - Revenue: {Revenue}, Growth: {Growth}",
                            revenueReport.TotalRevenue,
                            growthReport.TotalActiveSubscribers
                        );

            // Step 3: Extract Data (استخراج البيانات)

            // 3.1: من Revenue Report
            decimal totalRevenue = revenueReport.TotalRevenue;
            int unpaidInvoices = revenueReport.UnpaidInvoicesCount;
            decimal unpaidAmount = revenueReport.UnpaidAmount;

            // 3.2: من Growth Report
            int totalActiveSubscribers = growthReport.TotalActiveSubscribers;
            int newSubscribersThisMonth = growthReport.NewSubscribers;
            decimal growthRate = growthReport.GrowthRate;

            // 3.3: من Expiring Soon Report
            int expiringSoon = expiringReport.ExpiringSoon;
            int expiringIn3Days = expiringReport.ExpiringIn3Days;
            decimal potentialLoss = expiringReport.PotentialRevenueLoss;

            // 3.4: من Plan Popularity Report (Top Plan)
            var topPlan = popularityReport.Plans.FirstOrDefault();

            string topPlanName = "N/A";
            int topPlanSubscribers = 0;
            decimal topPlanPercentage = 0;
            decimal topPlanRevenue = 0;

            if (topPlan != null)
            {
                topPlanName = topPlan.planName;
                topPlanSubscribers = topPlan.SubscribersCount;
                topPlanPercentage = topPlan.Percentage;
                topPlanRevenue = topPlan.MonthlyRevenue;
            }

            // Step 4: Build & Return DTO
            var dashboard = new DashboardSummaryDto
            {
                // Revenue Metrics
                TotalRevenue = totalRevenue,
                UnpaidInvoices = unpaidInvoices,
                UnpaidAmount = unpaidAmount,

                // Subscribers Metrics
                TotalActiveSubscribers = totalActiveSubscribers,
                NewSubscribersThisMonth = newSubscribersThisMonth,
                GrowthRate = growthRate,

                // Expiring Metrics
                ExpiringSoon = expiringSoon,
                ExpiringIn3Days = expiringIn3Days,
                PotentialLoss = potentialLoss,

                // Top Plan
                TopPlanName = topPlanName,
                TopPlanSubscribers = topPlanSubscribers,
                TopPlanPercentage = topPlanPercentage,
                TopPlanRevenue = topPlanRevenue
            };

            _logger.LogInformation(
                "Dashboard Summary ready - Revenue: {Revenue}, Subscribers: {Subs}, Expiring: {Expiring}",
                dashboard.TotalRevenue,
                dashboard.TotalActiveSubscribers,
                dashboard.ExpiringSoon
            );

            return dashboard;
        }
    }
}