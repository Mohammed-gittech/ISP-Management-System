// ============================================
// CreatePlanValidator.cs
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Plans;

namespace ISP.Application.Validators
{
    public class CreatePlanValidator : AbstractValidator<CreatePlanDto>
    {
        public CreatePlanValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم الباقة مطلوب").MaximumLength(50);

            RuleFor(x => x.Speed)
                .GreaterThan(0)
                .WithMessage("السرعة يجب أن تكون أكبر من صفر")
                .LessThanOrEqualTo(1000)
                .WithMessage("السرعة يجب أن لا تتجاوز 1000 Mbps");

            RuleFor(x => x.Price).GreaterThan(0).WithMessage("السعر يجب أن يكون أكبر من صفر");

            RuleFor(x => x.DurationDays)
                .GreaterThan(0)
                .WithMessage("المدة يجب أن تكون أكبر من صفر")
                .LessThanOrEqualTo(365)
                .WithMessage("المدة يجب أن لا تتجاوز 365 يوم");
        }
    }
}
