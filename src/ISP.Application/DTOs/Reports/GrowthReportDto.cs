
namespace ISP.Application.DTOs.Reports
{
    public class GrowthReportDto
    {
        public int TotalActiveSubscribers { get; set; } // إجمالي المشتركين النشطين حالياً
        public int TotalAllSubscribers { get; set; } // إجمالي كل المشتركين (نشطين + غير نشطين)
        public int NewSubscribers { get; set; } // عدد المشتركين الجدد في الفترة المحددة
        public int ChurnedSubscribers { get; set; } // عدد المشتركين الذين ألغوا
        public int NetGrowth { get; set; } // صافي النمو (Net Growth)
        public decimal GrowthRate { get; set; } // معدل النمو بالنسبة المئوية
        public decimal ChurnRate { get; set; } // معدل churn بالنسبة المئوية
        public List<MonthlyGrowthDto> MonthlyTrend { get; set; } = new(); // عدد المشتركين شهر بشهر

    }

    public class MonthlyGrowthDto
    {
        public string Month { get; set; } = string.Empty; // "2026-1" مثال
        public int ActiveCount { get; set; } // عدد المشتركين النشطين في نهاية هذا الشهر
        public int NewCount { get; set; } // عدد المشتركين الجدد في هذا الشهر
        public int ChurnedCount { get; set; } // عدد الملغيين في هذا الشهر
        public int NetGrowth { get; set; } // النمو الصافي في هذا الشهر
    }
}