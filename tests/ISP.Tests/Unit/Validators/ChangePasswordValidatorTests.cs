// ============================================
// ChangePasswordValidatorTests.cs
// Unit Tests for ChangePasswordValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.DTOs.Users;
using ISP.Application.Validators;
using Microsoft.Extensions.Configuration;

namespace ISP.Tests.Unit.Validators
{
    public class ChangePasswordValidatorTests
    {
        private readonly ChangePasswordValidator _validator;

        private static IConfiguration BuildConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PasswordPolicy:MinimumLength"] = "8",
                    ["PasswordPolicy:MaximumLength"] = "128",
                    ["PasswordPolicy:RequireUppercase"] = "true",
                    ["PasswordPolicy:RequireLowercase"] = "true",
                    ["PasswordPolicy:RequireDigit"] = "true",
                    ["PasswordPolicy:RequireSpecialCharacter"] = "true"
                })
                .Build();

        public ChangePasswordValidatorTests()
        {
            _validator = new ChangePasswordValidator(BuildConfig());
        }

        private ChangePasswordDto CreateValidDto() => new ChangePasswordDto
        {
            OldPassword = "OldPass@123",
            NewPassword = "NewPass@456",
            ConfirmPassword = "NewPass@456"
        };

        // ============================================
        // Valid Data Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithValidData_ShouldPass()
        {
            var result = _validator.TestValidate(CreateValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // OldPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithEmptyOldPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.OldPassword = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.OldPassword)
                .WithErrorMessage("كلمة المرور القديمة مطلوبة");
        }

        // OldPassword لا تخضع لـ Policy
        // لأن المستخدم قد أنشأ حسابه قبل تطبيق الـ Policy الجديدة
        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithWeakOldPassword_ShouldPass()
        {
            var dto = CreateValidDto();
            dto.OldPassword = "weak"; // كلمة مرور ضعيفة قديمة

            var result = _validator.TestValidate(dto);

            // OldPassword لا تُطبَّق عليها الـ Policy → يجب أن تمر
            result.ShouldNotHaveValidationErrorFor(x => x.OldPassword);
        }

        // ============================================
        // NewPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithEmptyNewPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.NewPassword = string.Empty;
            dto.ConfirmPassword = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور الجديدة مطلوبة");
        }

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithNewPasswordWithoutUppercase_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "newpass@456";
            dto.ConfirmPassword = "newpass@456";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");
        }

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithNewPasswordWithoutSpecialChar_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "NewPass456";
            dto.ConfirmPassword = "NewPass456";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }

        // ============================================
        // ConfirmPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithNonMatchingConfirmPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.ConfirmPassword = "Different@789"; // لا يطابق NewPassword

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                .WithErrorMessage("كلمة المرور غير متطابقة");
        }

        // ============================================
        // NewPassword = OldPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ChangePasswordValidator")]
        public void Validate_WithNewPasswordSameAsOld_ShouldFailWithMessage()
        {
            var dto = new ChangePasswordDto
            {
                OldPassword = "Admin@123",
                NewPassword = "Admin@123", // نفس القديمة
                ConfirmPassword = "Admin@123"
            };

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور الجديدة يجب أن تكون مختلفة عن القديمة");
        }
    }

    // ============================================
    // ResetPasswordValidatorTests.cs
    // Unit Tests for ResetPasswordValidator
    // ============================================

    public class ResetPasswordValidatorTests
    {
        private readonly ResetPasswordValidator _validator;

        private static IConfiguration BuildConfig() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PasswordPolicy:MinimumLength"] = "8",
                    ["PasswordPolicy:MaximumLength"] = "128",
                    ["PasswordPolicy:RequireUppercase"] = "true",
                    ["PasswordPolicy:RequireLowercase"] = "true",
                    ["PasswordPolicy:RequireDigit"] = "true",
                    ["PasswordPolicy:RequireSpecialCharacter"] = "true"
                })
                .Build();

        public ResetPasswordValidatorTests()
        {
            _validator = new ResetPasswordValidator(BuildConfig());
        }

        private ResetPasswordDto CreateValidDto() => new ResetPasswordDto
        {
            NewPassword = "NewPass@123",
            ConfirmPassword = "NewPass@123"
        };

        // ============================================
        // Valid Data Tests
        // ============================================

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithValidData_ShouldPass()
        {
            var result = _validator.TestValidate(CreateValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // NewPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithEmptyNewPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.NewPassword = string.Empty;
            dto.ConfirmPassword = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور الجديدة مطلوبة");
        }

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithPasswordShorterThan8Chars_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "Aa1!aaa"; // 7 أحرف
            dto.ConfirmPassword = "Aa1!aaa";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تكون على الأقل 8 حرفاً");
        }

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithPasswordWithoutUppercase_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "newpass@123";
            dto.ConfirmPassword = "newpass@123";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");
        }

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithPasswordWithoutDigit_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "NewPass@abc";
            dto.ConfirmPassword = "NewPass@abc";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل (0-9)");
        }

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithPasswordWithoutSpecialChar_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.NewPassword = "NewPass123";
            dto.ConfirmPassword = "NewPass123";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.NewPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }

        // ============================================
        // ConfirmPassword Tests
        // ============================================

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithEmptyConfirmPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.ConfirmPassword = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                .WithErrorMessage("تأكيد كلمة المرور مطلوب");
        }

        [Fact]
        [Trait("Category", "ResetPasswordValidator")]
        public void Validate_WithNonMatchingConfirmPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.ConfirmPassword = "Different@789";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
                .WithErrorMessage("كلمة المرور غير متطابقة");
        }
    }
}