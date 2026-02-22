// ============================================
// ConfirmTenantPaymentDto.cs
// تأكيد استلام الدفع من SuperAdmin
// ============================================

namespace ISP.Application.DTOs.Tenants
{
    public class ConfirmTenantPaymentDto
    {
        // رقم TenantSubscription المعلق
        // Tenant قد يكون له أكثر من طلب معلق
        public int SubscriptionId { get; set; }

        // طريقة الدفع: "Bank Transfer", "Cash", "ZainCash"
        public string PaymentMethod { get; set; } = string.Empty;

        // رقم العملية من البنك — اختياري
        // مفيد للمراجعة المالية لاحقاً
        public string? TransactionId { get; set; }

        // ملاحظات إضافية — اختياري
        // مثال: "دفع نقدي في المكتب"
        public string? Notes { get; set; }
    }
}