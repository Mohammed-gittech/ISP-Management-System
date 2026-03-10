// ============================================
// ChangePasswordValidator.cs - التحقق من تغيير كلمة المرور
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Users;
using Microsoft.Extensions.Configuration;

namespace ISP.Application.Validators
{
    public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordValidator(IConfiguration configuration)
        {
            // OldPassword: إجباري
            RuleFor(x => x.OldPassword)
                .NotEmpty().WithMessage("كلمة المرور القديمة مطلوبة");

            // NewPassword: إجباري، 6+ أحرف
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
                .SetValidator(new PasswordPolicyValidator(configuration));

            // ConfirmPassword: يجب أن يطابق NewPassword
            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword).WithMessage("كلمة المرور غير متطابقة");

            // NewPassword: يجب أن يكون مختلف عن OldPassword
            RuleFor(x => x.NewPassword)
                .NotEqual(x => x.OldPassword)
                .WithMessage("كلمة المرور الجديدة يجب أن تكون مختلفة عن القديمة");
        }
    }
}