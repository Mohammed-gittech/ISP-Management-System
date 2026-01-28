// ============================================
// ChangePasswordValidator.cs - التحقق من تغيير كلمة المرور
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Users;

namespace ISP.Application.Validators
{
    public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
    {
        public ChangePasswordValidator()
        {
            // OldPassword: إجباري
            RuleFor(x => x.OldPassword)
                .NotEmpty().WithMessage("كلمة المرور القديمة مطلوبة");

            // NewPassword: إجباري، 6+ أحرف
            RuleFor(x => x.NewPassword)
                .NotEmpty().WithMessage("كلمة المرور الجديدة مطلوبة")
                .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");

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