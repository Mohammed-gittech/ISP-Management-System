// ============================================
// ResetPasswordValidator.cs - التحقق من إعادة تعيين كلمة المرور
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Users;
using Microsoft.Extensions.Configuration;

namespace ISP.Application.Validators
{
    public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
    {
        public ResetPasswordValidator(IConfiguration configuration)
        {
            // NewPassword: إجباري + سياسة كلمة المرور الكاملة
            // Admin يعيّن كلمة مرور جديدة → يجب أن تكون قوية
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
                .SetValidator(new PasswordPolicyValidator(configuration));

            // ConfirmPassword: يجب أن يطابق NewPassword
            // يمنع Admin من الخطأ في الكتابة
            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("تأكيد كلمة المرور مطلوب")
                .Equal(x => x.NewPassword).WithMessage("كلمة المرور غير متطابقة");
        }
    }
}