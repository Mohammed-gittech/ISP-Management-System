// ============================================
// UpdateUserValidator.cs - التحقق من تعديل بيانات المستخدم
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Users;

namespace ISP.Application.Validators
{
    public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserValidator()
        {
            // Username: اختياري، لكن إذا أرسل يجب أن يكون 3-50 حرف
            RuleFor(u => u.Username)
                .Length(3, 50).When(u => !string.IsNullOrEmpty(u.Username))
                .WithMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");

            // Email: اختياري، لكن إذا أرسل يجب أن يكون صحيح
            RuleFor(u => u.Email)
                .EmailAddress().When(u => !string.IsNullOrEmpty(u.Email))
                .WithMessage("صيغة البريد الإلكتروني غير صحيحة");
        }
    }
}