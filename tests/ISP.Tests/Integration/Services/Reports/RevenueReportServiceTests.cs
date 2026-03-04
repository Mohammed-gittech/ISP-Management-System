using FluentAssertions;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Services;
using ISP.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISP.Tests.Integration.Services.Reports
{
    public class RevenueReportServiceTests : IDisposable
    {

        // ============================================
        // Dependencies
        // ============================================

        private readonly ApplicationDbContext _context;
        private readonly ReportService _service;
        private readonly TestCurrentTenantService _tenantService;

        private readonly DateTime _startDate;
        private readonly DateTime _endDate;

        public RevenueReportServiceTests()
        {
            // 1. Create Context + Seed
            _context = ReportTestHelper.CreateInMemoryContext();
            ReportTestHelper.SeedReportData(_context);

            // 2. Create TenantService
            _tenantService = new TestCurrentTenantService();
            _tenantService.SetTenant(1); // Tenant 1

            // 3. Create Logger (NullLogger - لا يفعل شيء)
            var logger = NullLogger<ReportService>.Instance;

            (_startDate, _endDate) = ReportTestHelper.GetCurrentMonth();

            // 4. Create ReportService
            _service = new ReportService(
                _context,
                _tenantService,
                logger
            );
        }



        // TEST 1: TotalRevenue
        [Fact]
        public async Task GetRevenueReportAsync_WithValidPayments_ShouldReturnCorrectTotalRevenue()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.TotalRevenue.Should().Be(55000);
        }

        // TEST 2: PaidInvoicesCount
        [Fact]
        public async Task GetRevenueReportAsync_WithValidInvoices_ShouldReturnCorrectPaidInvoicesCount()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.PaidInvoicesCount.Should().Be(3);
        }

        // TEST 3: UnpaidInvoicesCount
        [Fact]
        public async Task GetRevenueReportAsync_WithValidInvoices_ShouldReturnCorrectUnpaidInvoicesCount()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.UnpaidInvoicesCount.Should().Be(2);
        }

        // TEST 4: UnpaidAmount
        [Fact]
        public async Task GetRevenueReportAsync_WithValidInvoices_ShouldReturnCorrectUnpaidAmount()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.UnpaidAmount.Should().Be(25000);
        }

        // TEST 5: MonthlyRevenue 
        [Fact]
        public async Task GetRevenueReportAsync_WithValidInvoices_ShouldReturnCorrectMonthlyRevenue()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync();

            // Assert 
            report.Should().NotBeNull();
            report.MonthlyRevenues.Count.Should().Be(1);
            report.MonthlyRevenues[0].Amount.Should().Be(55000);
        }

        // TEST 6: MonthlyRevenue 
        [Fact]
        public async Task GetRevenueReportAsync_WithValidPlans_ShouldReturnCorrectRevenueByPlan()
        {
            // Act 
            var report = await _service.GetRevenueReportAsync();

            // Assert 
            report.Should().NotBeNull();
            report.RevenueByPlan.Count.Should().Be(3);
            report.RevenueByPlan[0].SubscribersCount.Should().Be(2);
        }


        // ============================================
        // Cleanup: Dispose
        // ============================================
        public void Dispose()
        {
            // حذف In-Memory Database
            _context.Database.EnsureDeleted();

            // تنضيف Context 
            _context.Dispose();
        }
    }
}