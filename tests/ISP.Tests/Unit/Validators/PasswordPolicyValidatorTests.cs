// ============================================
// PasswordPolicyValidatorTests.cs
// Unit Tests for PasswordPolicyValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.Validators;
using Microsoft.Extensions.Configuration;

namespace ISP.Tests.Unit.Validators
{
    public class PasswordPolicyValidatorTests
    {
        // ============================================
        // إعداد الـ Configuration
        // ============================================

        // نستخدم ConfigurationBuilder لإنشاء IConfiguration وهمي
        // بدلاً من قراءة appsettings.json الحقيقي
        // هكذا الـ Tests مستقلة تماماً عن الملفات الخارجية
        private static IConfiguration BuildConfig(
            int minLength = 8,
            int maxLength = 128,
            bool requireUppercase = true,
            bool requireLowercase = true,
            bool requireDigit = true,
            bool requireSpecial = true)
        {
            // AddInMemoryCollection = نُمرّر Dictionary كـ appsettings وهمي
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PasswordPolicy:MinimumLength"] = minLength.ToString(),
                    ["PasswordPolicy:MaximumLength"] = maxLength.ToString(),
                    ["PasswordPolicy:RequireUppercase"] = requireUppercase.ToString(),
                    ["PasswordPolicy:RequireLowercase"] = requireLowercase.ToString(),
                    ["PasswordPolicy:RequireDigit"] = requireDigit.ToString(),
                    ["PasswordPolicy:RequireSpecialCharacter"] = requireSpecial.ToString()
                })
                .Build();
        }

        // Default validator — يستخدم الإعدادات الافتراضية في كل الـ Tests
        private readonly PasswordPolicyValidator _validator;

        public PasswordPolicyValidatorTests()
        {
            _validator = new PasswordPolicyValidator(BuildConfig());
        }

        // ============================================
        // Valid Password Tests
        // ============================================

        // Test 1: كلمة مرور قوية كاملة تمر
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithStrongPassword_ShouldPass()
        {
            var result = _validator.TestValidate("Admin@123");
            result.ShouldNotHaveAnyValidationErrors();
        }

        // Test 2: Theory — كل الكلمات القوية تمر
        [Theory]
        [Trait("Category", "PasswordPolicy")]
        [InlineData("Admin@123")]      // كلاسيكية
        [InlineData("P@ssw0rd")]       // مختلفة
        [InlineData("Str0ng!Pass")]    // طويلة
        [InlineData("Aa1!aaaa")]       // الحد الأدنى بالضبط
        public void Validate_WithVariousStrongPasswords_ShouldPass(string password)
        {
            var result = _validator.TestValidate(password);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // MinimumLength Tests
        // ============================================

        // Test 3: أقل من 8 أحرف → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithPasswordShorterThan8_ShouldFailWithMessage()
        {
            // "Aa1!aaa" = 7 أحرف — يحتوي على كل المتطلبات لكن قصير جداً
            var result = _validator.TestValidate("Aa1!aaa");

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن تكون على الأقل 8 حرفاً");
        }

        // Test 4: بالضبط 8 أحرف → يمر
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithPasswordExactly8Chars_ShouldPass()
        {
            // "Aa1!aaaa" = 8 أحرف بالضبط
            var result = _validator.TestValidate("Aa1!aaaa");
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // MaximumLength Tests
        // ============================================

        // Test 5: أكثر من 128 حرف → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithPasswordLongerThan128_ShouldFailWithMessage()
        {
            // 129 حرف — نضمن وجود كل المتطلبات في البداية
            var password = "Admin@123" + new string('a', 120);
            // "Admin@123" = 9 أحرف + 120 = 129 حرف

            var result = _validator.TestValidate(password);

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن لا تتجاوز 128 حرفاً");
        }

        // Test 6: بالضبط 128 حرف → يمر
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithPasswordExactly128Chars_ShouldPass()
        {
            // 128 حرف بالضبط مع كل المتطلبات
            var password = "Admin@123" + new string('a', 119);
            // 9 + 119 = 128 حرف

            var result = _validator.TestValidate(password);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // RequireUppercase Tests
        // ============================================

        // Test 7: بدون Uppercase → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutUppercase_ShouldFailWithMessage()
        {
            // "admin@123" = لا يوجد حرف كبير
            var result = _validator.TestValidate("admin@123");

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");
        }

        // Test 8: RequireUppercase = false → يمر بدون Uppercase
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutUppercaseWhenNotRequired_ShouldPass()
        {
            // نُنشئ Validator بدون شرط Uppercase
            var validator = new PasswordPolicyValidator(
                BuildConfig(requireUppercase: false));

            var result = validator.TestValidate("admin@123");

            // لا يجب أن يفشل بسبب Uppercase
            result.ShouldNotHaveValidationErrorFor(x => x);
        }

        // ============================================
        // RequireLowercase Tests
        // ============================================

        // Test 9: بدون Lowercase → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutLowercase_ShouldFailWithMessage()
        {
            // "ADMIN@123" = لا يوجد حرف صغير
            var result = _validator.TestValidate("ADMIN@123");

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف صغير على الأقل (a-z)");
        }

        // Test 10: RequireLowercase = false → يمر بدون Lowercase
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutLowercaseWhenNotRequired_ShouldPass()
        {
            var validator = new PasswordPolicyValidator(
                BuildConfig(requireLowercase: false));

            var result = validator.TestValidate("ADMIN@123");

            result.ShouldNotHaveValidationErrorFor(x => x);
        }

        // ============================================
        // RequireDigit Tests
        // ============================================

        // Test 11: بدون رقم → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutDigit_ShouldFailWithMessage()
        {
            // "Admin@abc" = لا يوجد رقم
            var result = _validator.TestValidate("Admin@abc");

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل (0-9)");
        }

        // Test 12: RequireDigit = false → يمر بدون رقم
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutDigitWhenNotRequired_ShouldPass()
        {
            var validator = new PasswordPolicyValidator(
                BuildConfig(requireDigit: false));

            var result = validator.TestValidate("Admin@abc");

            result.ShouldNotHaveValidationErrorFor(x => x);
        }

        // ============================================
        // RequireSpecialCharacter Tests
        // ============================================

        // Test 13: بدون رمز خاص → يفشل
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutSpecialChar_ShouldFailWithMessage()
        {
            // "Admin1234" = لا يوجد رمز خاص
            var result = _validator.TestValidate("Admin1234");

            result.ShouldHaveValidationErrorFor(x => x)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }

        // Test 14: RequireSpecialCharacter = false → يمر بدون رمز خاص
        [Fact]
        [Trait("Category", "PasswordPolicy")]
        public void Validate_WithoutSpecialCharWhenNotRequired_ShouldPass()
        {
            var validator = new PasswordPolicyValidator(
                BuildConfig(requireSpecial: false));

            var result = validator.TestValidate("Admin1234");

            result.ShouldNotHaveValidationErrorFor(x => x);
        }

        // Test 15: Theory — كل الرموز الخاصة المدعومة تمر
        [Theory]
        [Trait("Category", "PasswordPolicy")]
        [InlineData("Admin@123")]   // @
        [InlineData("Admin#123")]   // #
        [InlineData("Admin!123")]   // !
        [InlineData("Admin$123")]   // $
        [InlineData("Admin%123")]   // %
        [InlineData("Admin^123")]   // ^
        [InlineData("Admin&123")]   // &
        [InlineData("Admin*123")]   // *
        public void Validate_WithVariousSpecialChars_ShouldPass(string password)
        {
            var result = _validator.TestValidate(password);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Multiple Errors Tests
        // ============================================

        // Test 16: كلمة مرور ضعيفة جداً → أخطاء متعددة
        // [Fact]
        // [Trait("Category", "PasswordPolicy")]
        // public void Validate_WithVeryWeakPassword_ShouldFailWithMultipleErrors()
        // {
        //     // "abc" = قصيرة + لا Uppercase + لا Digit + لا Special
        //     var result = _validator.TestValidate("abc");

        //     // يجب أن تكون هناك أخطاء متعددة
        //     result.ShouldHaveValidationErrorFor(x => x);
        //     result.Errors.Should().HaveCountGreaterThan(1);
        // }
    }
}