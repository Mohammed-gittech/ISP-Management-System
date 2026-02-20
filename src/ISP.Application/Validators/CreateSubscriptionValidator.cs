// ============================================
// CreateSubscriptionValidator.cs
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Subscriptions;

namespace ISP.Application.Validators
{
    public class CreateSubscriptionValidator : AbstractValidator<CreateSubscriptionDto>
    {
        public CreateSubscriptionValidator()
        {
            RuleFor(x => x.SubscriberId).GreaterThan(0).WithMessage("يجب اختيار المشترك");

            RuleFor(x => x.PlanId).GreaterThan(0).WithMessage("يجب اختيار الباقة");

            RuleFor(x => x.StartDate)
                .NotEmpty()
                .WithMessage("تاريخ البدء مطلوب")
                .GreaterThanOrEqualTo(ـ => DateTime.UtcNow.Date)
                .WithMessage("تاريخ البدء لا يمكن أن يكون في الماضي");
        }
    }
}
