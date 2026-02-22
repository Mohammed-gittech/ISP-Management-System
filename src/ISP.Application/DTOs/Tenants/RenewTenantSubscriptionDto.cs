// ============================================
// RenewTenantSubscriptionDto.cs
// طلب تجديد اشتراك الوكيل
// ============================================

using ISP.Domain.Enums;

namespace ISP.Application.DTOs.Tenants
{
    public class RenewTenantSubscriptionDto
    {
        // الباقة المطلوبة للتجديد
        public TenantPlan Plan { get; set; }

        // المدة بالأشهر
        // Free  → 1 شهر فقط
        // Basic/Pro → بين 1 و 12 شهر
        public int DurationMonths { get; set; } = 1;
    }
}