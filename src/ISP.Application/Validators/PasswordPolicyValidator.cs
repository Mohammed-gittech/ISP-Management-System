// ============================================
// PasswordPolicyValidator.cs
// Validator مستقل لسياسة كلمة المرور
// يُعاد استخدامه في كل Validator يحتاجه
// ============================================
using FluentValidation;
using Microsoft.Extensions.Configuration;

namespace ISP.Application.Validators
{
    /// <summary>
    /// Validator مستقل لقواعد كلمة المرور
    /// الاستخدام:
    ///   RuleFor(x => x.Password).SetValidator(new PasswordPolicyValidator(_configuration));
    /// </summary>
    public class PasswordPolicyValidator : AbstractValidator<string>
    {

        public PasswordPolicyValidator(IConfiguration configuration)
        {
            // قراءة الإعدادات من appsettings.json
            // لو لم يُوجد المفتاح → نستخدم القيمة الافتراضية الآمنة
            var minLength = configuration
                .GetValue<int>("PasswordPolicy:MinimumLength", 8);

            var maxLength = configuration
                .GetValue<int>("PasswordPolicy:MaximumLength", 128);

            var requireUppercase = configuration
                .GetValue<bool>("PasswordPolicy:RequireUppercase", true);

            var requireLowercase = configuration
                .GetValue<bool>("PasswordPolicy:RequireLowercase", true);

            var requireDigit = configuration
                .GetValue<bool>("PasswordPolicy:RequireDigit", true);

            var requireSpecial = configuration
                .GetValue<bool>("PasswordPolicy:RequireSpecialCharacter", true);


            // ============================
            // القاعدة 1 — الحد الأدنى للطول
            // ============================
            // MinimumLength يتحقق من عدد الأحرف
            // WithMessage يستخدم {MinLength} و {TotalLength} كـ Placeholders
            // FluentValidation يستبدلها تلقائياً بالقيم الحقيقية
            RuleFor(x => x)
                .MinimumLength(minLength)
                .WithMessage("كلمة المرور يجب أن تكون على الأقل {MinLength} حرفاً");

            // ============================
            // القاعدة 2 — الحد الأقصى للطول
            // ============================
            // نمنع كلمات المرور الطويلة جداً
            // bcrypt يعاني مع أكثر من 72 حرف
            // 128 = حد آمن ومريح للمستخدم
            RuleFor(x => x)
                .MaximumLength(maxLength)
                .WithMessage($"كلمة المرور يجب أن لا تتجاوز {maxLength} حرفاً");

            // ============================
            // القاعدة 3 — حرف كبير
            // ============================
            // When(requireUppercase) = طبّق القاعدة فقط لو الإعداد = true
            // لو RequireUppercase = false في appsettings → هذه القاعدة تُتجاهَل
            // يعطيك مرونة لتخفيف القواعد بدون تعديل الكود
            if (requireUppercase)
                RuleFor(x => x)
                    .Matches("[A-Z]")
                    // Matches = Regex — يبحث عن حرف كبير واحد على الأقل
                    .WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");

            // ============================
            // القاعدة 4 — حرف صغير
            // ============================
            if (requireLowercase)
                RuleFor(x => x)
                    .Matches("[a-z]")
                    .WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير على الأقل (a-z)");

            // ============================
            // القاعدة 5 — رقم
            // ============================
            if (requireDigit)
                RuleFor(x => x)
                    .Matches("[0-9]")
                    .WithMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل (0-9)");

            // ============================
            // القاعدة 6 — رمز خاص
            // ============================
            // [!@#$%^&*] = قائمة الرموز المقبولة
            // يمكن إضافة رموز أخرى مستقبلاً مثل: ()-_+=
            if (requireSpecial)
                RuleFor(x => x)
                    .Matches(@"[!@#$%^&*()_\-+=\[\]{}|;:,.<>?/\\]")
                    .WithMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }
    }
}