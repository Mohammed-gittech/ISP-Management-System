// ============================================
// CreateUserValidator.cs - التحقق من بيانات المستخدم الجديد
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Users;

namespace ISP.Application.Validators
{
    public class CreateUserValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserValidator()
        {
            // Username: إجباري، 3-50 حرف
            RuleFor(u => u.Username)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .Length(3, 50).WithMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");

            // Email: إجباري، صيغة صحيحة
            RuleFor(u => u.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة");

            // Password: إجباري، 6+ أحرف
            RuleFor(u => u.Password)
                .NotEmpty().WithMessage("كلمة المرور مطلوبة")
                .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");

            // Role: إجباري، من القائمة المسموحة
            RuleFor(u => u.Role)
                .NotEmpty().WithMessage("الدور مطلوب")
                .Must(role => role == "SuperAdmin" || role == "TenantAdmin" || role == "Employee")
                .WithMessage("الدور يجب أن يكون: SuperAdmin, TenantAdmin, أو Employee");

            // TenantId: إجباري إذا لم يكن SuperAdmin
            RuleFor(u => u.TenantId)
                .NotNull().When(u => u.Role != "SuperAdmin")
                .WithMessage("معرف الوكيل مطلوب للأدوار غير SuperAdmin");

            // TenantId: يجب أن يكون null إذا كان SuperAdmin
            RuleFor(u => u.TenantId)
                .Null().When(u => u.Role == "SuperAdmin")
                .WithMessage("SuperAdmin لا يحتاج معرف وكيل");
        }
    }
}