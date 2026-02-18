namespace ISP.Application.DTOs.Reports
{
    public class ExpiringSubscriptionDto
    {
        // معلومات أساسية
        public int SubscriptionId { get; set; } // معرف الاشتراك

        public string SubscriberName { get; set; } = string.Empty; // اسم المشترك
        public string SubscriberPhone { get; set; } = string.Empty; // رقم هاتف المشترك

        // معلومات الباقة
        public string PlanName { get; set; } = string.Empty; // اسم الباقة
        public decimal Price { get; set; } // سعر الباقة الشهري

        // معلومات الانتهاء
        public DateTime EndDate { get; set; } // تاريخ انتهاء الاشتراك
        public int DaysRemaining { get; set; } // الأيام المتبقية حتى الانتهاء
        public string Status { get; set; } = string.Empty; // حالة الاشتراك
        public string Priority { get; set; } = string.Empty; // أولوية المتابعة
    }
}