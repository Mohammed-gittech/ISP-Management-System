namespace ISP.Application.DTOs.Reports
{
    public class PlanPopularityReportDto
    {
        public int TotalSubscribers { get; set; } // إجمالي عدد المشتركين النشطين
        public int TotalPlans { get; set; } // عدد الباقات المتاحة (اللي عندها مشتركين)
        public decimal TotalMonthlyRevenue { get; set; } // إجمالي الإيرادات الشهرية من كل الباقات
        public decimal TotalAnnualRevenue { get; set; } // إجمالي الإيرادات السنوية المتوقعة

        public List<PlanPopularityDto> Plans { get; set; } = new(); // قائمة الباقات مع إحصائياتها
    }
}