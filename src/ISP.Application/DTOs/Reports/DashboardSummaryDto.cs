namespace ISP.Application.DTOs.Reports
{
    public class DashboardSummaryDto
    {
        // 1. Revenue Metrics
        public decimal TotalRevenue { get; set; } // إجمالي الإيرادات (الشهر الحالي أو الفترة المحددة)
        public int UnpaidInvoices { get; set; } // عدد الفواتير الغير مدفوعة
        public decimal UnpaidAmount { get; set; } // المبلغ الإجمالي الغير مدفوع

        // 2. Subscribers Metrics 
        public int TotalActiveSubscribers { get; set; } // إجمالي المشتركين النشطين حالياً
        public int NewSubscribersThisMonth { get; set; } // عدد المشتركين الجدد في الشهر الحالي
        public decimal GrowthRate { get; set; } // معدل النمو بالنسبة المئوية

        // 3. Expiring Metrics
        public int ExpiringSoon { get; set; } // عدد الاشتراكات المنتهية خلال 7 أيام
        public int ExpiringIn3Days { get; set; } // عدد الاشتراكات العاجلة (خلال 3 أيام)
        public decimal PotentialLoss { get; set; } // الخسارة المالية المحتملة (شهرياً)

        // 4. Top Plan
        public string TopPlanName { get; set; } = string.Empty; // اسم الباقة الأكثر شعبية
        public int TopPlanSubscribers { get; set; } // عدد المشتركين في الباقة الأشهر
        public decimal TopPlanPercentage { get; set; } // نسبة المشتركين في الباقة الأشهر
        public decimal TopPlanRevenue { get; set; } // الإيرادات الشهرية من الباقة الأشهر

    }
}