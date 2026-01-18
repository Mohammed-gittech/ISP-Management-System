// ============================================
// CreateTenantValidator.cs
// ============================================
using ISP.Application.DTOs.Tenants;
using FluentValidation;

namespace ISP.Application.Validators
{
    public class CreateTenantValidator : AbstractValidator<CreateTenantDto>
    {
        public CreateTenantValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الشركة مطلوب")
                .MaximumLength(100).WithMessage("اسم الشركة لا يمكن أن يتجاوز 100 حرف");

            RuleFor(x => x.ContactEmail)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
                .MaximumLength(100);

            RuleFor(x => x.ContactPhone)
                .MaximumLength(20).When(x => !string.IsNullOrEmpty(x.ContactPhone));

            RuleFor(x => x.AdminUserName)
                .NotEmpty().WithMessage("اسم مستخدم المسؤول مطلوب")
                .MaximumLength(3).WithMessage("اسم المستخدم يجب أن يكون 3 أحرف على الأقل")
                .MaximumLength(50);

            RuleFor(x => x.AdminEmail)
                .NotEmpty().WithMessage("البريد الإلكتروني للمدير مطلوب")
                .EmailAddress().WithMessage("البريد الإلكتروني غير صالح");

            RuleFor(x => x.AdminPassword)
                .NotEmpty().WithMessage("كلمة المرور للمسؤول مطلوبة")
                .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
        }
    }


}