// ============================================
// CreateSubscriberValidator.cs
// ============================================

using FluentValidation;
using ISP.Application.DTOs.Subscribers;

namespace ISP.Application.Validators
{
    public class CreateSubscriberValidator : AbstractValidator<CreateSubscriberDto>
    {
        public CreateSubscriberValidator()
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("الاسم الكامل مطلوب")
                .MinimumLength(3).WithMessage("الاسم يجب أن يكون على الأقل 3 أحرف")
                .MaximumLength(100).WithMessage("الاسم يجب أن لا يتجاوز 100 حرف");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("رقم الهاتف مطلوب")
                .Matches(@"^07[3-9]\d{8}$")
                .WithMessage("رقم الهاتف يجب أن يكون صالح (07XXXXXXXX)")
                .MaximumLength(20);

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Address)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Address));

            RuleFor(x => x.NationalId)
            .MaximumLength(20).When(x => !string.IsNullOrEmpty(x.NationalId));
        }
    }
}