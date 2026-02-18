
namespace ISP.Application.DTOs.Reports
{
    public class RevenueReportDto
    {
        public decimal TotalRevenue { get; set; } // اجمالي الدخل
        public int PaidInvoicesCount { get; set; } //عدد الفواتير المدفوعة 
        public int UnpaidInvoicesCount { get; set; } // عدد الفواتير الغير مدفوعة
        public decimal UnpaidAmount { get; set; } // المبلغ الغير مدفوع
        public List<MonthlyRevenueDto> MonthlyRevenues { get; set; } = new(); // تفاصيل شهرية
        public List<PlanRevenueDto> RevenueByPlan { get; set; } = new(); // تفاصيل حسب الباقة

    }

    public class MonthlyRevenueDto
    {
        public string Month { get; set; } = string.Empty; //"2026-01"
        public decimal Amount { get; set; } //15000
        public int InvoicesCount { get; set; } //50
    }

    public class PlanRevenueDto
    {
        public string PlanName { get; set; } = string.Empty; // "100 Mbps"
        public decimal Revenue { get; set; } // 27000.25
        public int SubscribersCount { get; set; } // 100
        public decimal Percentage { get; set; } //60%
    }
}