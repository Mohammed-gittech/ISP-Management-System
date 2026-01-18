// ============================================
// UpdateSubscriberValidator.cs
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Subscribers;

namespace ISP.Application.Validators
{
    public class UpdateSubscriberValidator : AbstractValidator<UpdateSubscriberDto>
    {
        public UpdateSubscriberValidator()
        {
            RuleFor(x => x.FullName)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.FullName));

            RuleFor(x => x.PhoneNumber)
                .Matches(@"^07[3-9]\d{8}$")
                .WithMessage("رقم الهاتف يجب أن يكون صالح (07XXXXXXXX)")
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
                .When(x => !string.IsNullOrEmpty(x.Email));
        }
    }
}