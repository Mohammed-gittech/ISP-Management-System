using FluentAssertions;
using ISP.Application.Interfaces;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Services;
using ISP.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISP.Tests.Integration.Services.Reports
{
    public class DashboardSummaryServiceTest : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly ReportService _service;
        private readonly ICurrentTenantService _tenantService;

        public DashboardSummaryServiceTest()
        {
            // Create Context + Seed
            _context = ReportTestHelper.CreateInMemoryContext();
            ReportTestHelper.SeedReportData(_context);

            // Create TenantService
            _tenantService = new TestCurrentTenantService();
            _tenantService.SetTenant(1);

            // Create Logger (NullLogger)
            var logger = NullLogger<ReportService>.Instance;

            // Create ReportService
            _service = new ReportService(
                _context,
                _tenantService,
                logger
            );
        }

        // TEST 1: Revenue Report 
        [Fact]
        public async Task GetDashBoardSummaryAsync_WithValidRevenueReport_ShouldReturnCorrectRevenueReport()
        {
            // Act 
            var dashboard = await _service.GetDashboardSummaryAsync();

            // Assert 
            dashboard.Should().NotBeNull();
            dashboard.TotalRevenue.Should().Be(55000);
            dashboard.UnpaidInvoices.Should().Be(2);
            dashboard.UnpaidAmount.Should().Be(25000);
        }

        // TEST 2: Growth Report 
        [Fact]
        public async Task GetDashboardSummaryAsync_WithValidGrowthReport_ShouldReturnCorrecttGrowthReport()
        {
            // Act
            var dashboard = await _service.GetDashboardSummaryAsync();

            // Assert 
            dashboard.Should().NotBeNull();
            dashboard.TotalActiveSubscribers.Should().Be(4);
            dashboard.NewSubscribersThisMonth.Should().Be(1);
            dashboard.GrowthRate.Should().Be(0);
        }

        // TEST 3: Expiring Soon Report
        [Fact]
        public async Task GetDashboardSummaryAsync_WithValidExpiringSoon_ShouldReturnCorrectExpiringSoon()
        {
            // Act
            var dashboard = await _service.GetDashboardSummaryAsync();

            // Assert 
            dashboard.Should().NotBeNull();
            dashboard.ExpiringSoon.Should().Be(2);
            dashboard.ExpiringIn3Days.Should().Be(2);
            dashboard.PotentialLoss.Should().Be(25000);
        }

        // TEST 4: Plan Popularity Report 
        [Fact]
        public async Task GetDashboardSummaryAsync_WithValidPlanPopularityReport_ShouldReturnCorrectTopPlan()
        {
            // Act
            var dashboard = await _service.GetDashboardSummaryAsync();

            // Assert 
            dashboard.Should().NotBeNull();
            dashboard.TopPlanName.Should().Be("Gold 100 Mbps");
            dashboard.TopPlanSubscribers.Should().Be(1);
            dashboard.TopPlanPercentage.Should().Be(100);
            dashboard.TopPlanRevenue.Should().Be(20000);
        }


        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}