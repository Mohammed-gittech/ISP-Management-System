// ============================================
// RenewTenantSubscriptionValidator.cs
// التحقق من صحة طلب تجديد اشتراك الوكيل
// ===========================================

using FluentValidation;
using ISP.Application.DTOs.Tenants;
using ISP.Domain.Enums;

namespace ISP.Application.Validators
{
    public class RenewTenantSubscriptionValidator : AbstractValidator<RenewTenantSubscriptionDto>
    {
        public RenewTenantSubscriptionValidator()
        {
            // Plan مطلوب ويجب أن يكون قيمة صالحة
            RuleFor(x => x.Plan)
                .IsInEnum()
                .WithMessage("الباقة المختارة غير صالحة");

            // Free Plan → شهر واحد فقط
            RuleFor(x => x.DurationMonths)
                .Equal(1)
                .When(x => x.Plan == TenantPlan.Free)
                .WithMessage("الباقة المجانية شهر واحد فقط");

            // Basic/Pro → بين 1 و 12 شهر
            RuleFor(x => x.DurationMonths)
                .GreaterThan(0)
                .WithMessage("المدة يجب أن تكون شهر واحد على الأقل")
                .LessThanOrEqualTo(12)
                .WithMessage("المدة لا يمكن أن تتجاوز 12 شهراً")
                .When(x => x.Plan != TenantPlan.Free);
        }
    }
}