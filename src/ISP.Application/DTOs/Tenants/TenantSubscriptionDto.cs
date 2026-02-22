// ============================================
// TenantSubscriptionDto.cs
// بيانات اشتراك الوكيل في النظام
// ============================================

namespace ISP.Application.DTOs.Tenants
{
    public class TenantSubscriptionDto
    {
        // رقم الاشتراك — مهم للـ ConfirmPaymentAsync
        public int Id { get; set; }

        public int TenantId { get; set; }

        // الباقة: Free, Basic, Pro
        public string Plan { get; set; } = string.Empty;

        // السعر الكلي (سعر الباقة × عدد الأشهر)
        public decimal Price { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // الحالة: Pending, Active, Expired, Suspended
        public string Status { get; set; } = string.Empty;

        public string? PaymentMethod { get; set; }
        public DateTime? LastPaymentDate { get; set; }

        // عدد الأيام المتبقية — مفيد للـ TenantAdmin
        public int DaysRemaining => (EndDate - DateTime.UtcNow).Days;

        // هل الاشتراك نشط؟
        public bool IsActive => Status == "Active" && EndDate > DateTime.UtcNow;
    }
}