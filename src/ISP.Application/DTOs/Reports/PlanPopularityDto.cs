
namespace ISP.Application.DTOs.Reports
{
    public class PlanPopularityDto
    {
        // معلومات أساسية
        public int PlanId { get; set; } // معرف الباقة
        public string planName { get; set; } = string.Empty; // اسم الباقة
        public int Speed { get; set; } // مثال: 100 (Mbps) السرعة بالميجابت 
        public decimal Price { get; set; } // السعر الشهري

        // مقاييس الشعبية
        public int SubscribersCount { get; set; } // عدد المشتركين في هذه الباقة
        public decimal Percentage { get; set; } // النسبة المئوية من إجمالي المشتركين
        public int Rank { get; set; } // الترتيب (1 = الأشهر)

        // مقاييس الإيرادات
        public decimal MonthlyRevenue { get; set; } // الإيرادات الشهرية من هذه الباقة
        public decimal AnnualRevenue { get; set; } // الإيرادات السنوية المتوقعة

    }
}