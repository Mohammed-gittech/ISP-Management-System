using FluentAssertions;
using ISP.Infrastructure.Data;
using ISP.Infrastructure.Services;
using ISP.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISP.Tests.Integration.Services.Reports
{
    public class GrowthReportServiceTests : IDisposable
    {
        // ============================================
        // Dependencies
        // ============================================

        private readonly ApplicationDbContext _context;
        private readonly ReportService _service;
        private readonly TestCurrentTenantService _tenantService;

        private readonly DateTime _startDate;
        private readonly DateTime _endDate;

        // ============================================
        // Constructor: Setup
        // ============================================

        public GrowthReportServiceTests()
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

        // TEST 1: TotalActiveSubscribers
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesTotalActiveSubscribers_Correctly()
        {

            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.TotalActiveSubscribers.Should().Be(4);
        }

        // TEST 2: NewSubscribers
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesNewSubscribers_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.NewSubscribers.Should().Be(1);
        }

        // TEST 3: TotalAllSubscribers
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesTotalAllSubscribers_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.TotalAllSubscribers.Should().Be(5);
        }

        // TEST 4: Churned Subscribers
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesChurnedSubscribers_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.ChurnedSubscribers.Should().Be(1);
        }

        // Test 5: Net Growth
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesNetGrowth_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.NetGrowth.Should().Be(0);
        }

        // Test 6: Growth Rate
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesGrowthRate_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.GrowthRate.Should().Be(0);
        }

        // TEST 7: Churn Rate
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesChurnRate_Correctly()
        {
            // Act 
            var report = await _service.GetGrowthReportAsync(_startDate, _endDate);

            // Assert 
            report.Should().NotBeNull();
            report.ChurnRate.Should().Be(20);
        }

        // TEST 8: MonthlyTrend
        [Fact]
        public async Task GetGrowthReportAsync_CalculatesMonthlyTrend_Correctly()
        {
            // Arrange
            // Act 
            var report = await _service.GetGrowthReportAsync();

            // Assert 
            report.Should().NotBeNull();
            report.MonthlyTrend.Count.Should().Be(3);
        }


        // ============================================
        // Cleanup: Dispose
        // ============================================

        public void Dispose()
        {
            // حذف In-Memory Database
            _context.Database.EnsureDeleted();

            // تنظيف Context
            _context.Dispose();
        }
    }
}