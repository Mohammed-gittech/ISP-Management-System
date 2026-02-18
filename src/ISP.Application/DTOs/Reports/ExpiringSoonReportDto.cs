namespace ISP.Application.DTOs.Reports
{
    public class ExpiringSoonReportDto
    {
        // إحصائيات
        public int ExpiringSoon { get; set; } // إجمالي عدد الاشتراكات المنتهية قريباً (خلال 7 أيام)
        public int ExpiringIn1Day { get; set; } // عدد الاشتراكات المنتهية خلال يوم واحد
        public int ExpiringIn3Days { get; set; } // عدد الاشتراكات المنتهية خلال 3 أيام
        public int ExpiringIn7Days { get; set; } // عدد الاشتراكات المنتهية خلال 7 أيام

        // معلومات إضافية
        public int AlreadyExpired { get; set; } // عدد الاشتراكات المنتهية بالفعل
        public decimal PotentialRevenueLoss { get; set; } // الخسارة المالية المحتملة (شهرياً)

        // القائمة الكاملة
        public List<ExpiringSubscriptionDto> Subscriptions { get; set; } = new(); // قائمة الاشتراكات المنتهية قريباً

    }
}