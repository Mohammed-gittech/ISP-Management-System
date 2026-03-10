// ============================================
// CreateTenantValidatorTests.cs
// Unit Tests for CreateTenantValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Validators;
using ISP.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace ISP.Tests.Unit.Validators
{
    public class CreateTenantValidatorTests
    {
        private readonly CreateTenantValidator _validator;

        // ============================================
        // Helper — IConfiguration وهمي
        // ============================================

        private static IConfiguration BuildConfig(
            int minLength = 8,
            int maxLength = 128,
            bool requireUppercase = true,
            bool requireLowercase = true,
            bool requireDigit = true,
            bool requireSpecial = true)
        {
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

        public CreateTenantValidatorTests()
        {
            _validator = new CreateTenantValidator(BuildConfig());
        }

        // ============================================
        // Helper Method — بيانات صحيحة جاهزة
        // ============================================

        private CreateTenantDto CreateValidDto(TenantPlan plan = TenantPlan.Free) => new CreateTenantDto
        {
            Name = "شركة النور",
            ContactEmail = "info@alnoor.com",
            ContactPhone = "0501234567",
            SubscriptionPlan = plan,
            DurationMonths = plan == TenantPlan.Free ? 1 : 3,
            AdminUsername = "admin",
            AdminEmail = "admin@alnoor.com",
            AdminPassword = "Admin@123"
            // ← "Admin@123" تمر الآن: 8+ أحرف، Uppercase، Lowercase، Digit، Special
        };

        // ============================================
        // Valid Data Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithValidFreePlanData_ShouldPass()
        {
            var result = _validator.TestValidate(CreateValidDto(TenantPlan.Free));
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithValidBasicPlan3Months_ShouldPass()
        {
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 3;
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithValidProPlan12Months_ShouldPass()
        {
            var dto = CreateValidDto(TenantPlan.Pro);
            dto.DurationMonths = 12;
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Name Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithEmptyName_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Name = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorMessage("اسم الشركة مطلوب");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithNameExceeding100Characters_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.Name = new string('أ', 101);

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorMessage("اسم الشركة لا يمكن أن يتجاوز 100 حرف");
        }

        // ============================================
        // ContactEmail Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithEmptyContactEmail_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.ContactEmail = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.ContactEmail)
                .WithErrorMessage("البريد الإلكتروني مطلوب");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithInvalidContactEmail_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.ContactEmail = "not-an-email";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.ContactEmail)
                .WithErrorMessage("البريد الإلكتروني غير صالح");
        }

        // ============================================
        // AdminUsername Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithShortAdminUsername_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.AdminUsername = "ab";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.AdminUsername)
                .WithErrorMessage("اسم المستخدم يجب أن يكون 3 أحرف على الأقل");
        }

        // ============================================
        // AdminPassword Tests ← محدَّث بالكامل
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithEmptyAdminPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.AdminPassword = string.Empty;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
                .WithErrorMessage("كلمة المرور للمسؤول مطلوبة");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithAdminPasswordShorterThan8Chars_ShouldFail()
        {
            // ← تعديل: من 6 إلى 8 حسب الـ Policy الجديدة
            var dto = CreateValidDto();
            dto.AdminPassword = "Aa1!aaa"; // 7 أحرف

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
                .WithErrorMessage("كلمة المرور يجب أن تكون على الأقل 8 حرفاً");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithAdminPasswordWithoutUppercase_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.AdminPassword = "admin@123";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithAdminPasswordWithoutSpecialChar_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.AdminPassword = "Admin1234";

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }

        // ============================================
        // DurationMonths Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithFreePlanAndDuration1_ShouldPass()
        {
            var dto = CreateValidDto(TenantPlan.Free);
            dto.DurationMonths = 1;
            var result = _validator.TestValidate(dto);
            result.ShouldNotHaveValidationErrorFor(x => x.DurationMonths);
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithFreePlanAndDurationMoreThan1_ShouldFailWithMessage()
        {
            var dto = CreateValidDto(TenantPlan.Free);
            dto.DurationMonths = 6;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("الباقة المجانية شهر واحد فقط");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithBasicPlanAndDuration0_ShouldFailWithMessage()
        {
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 0;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("المدة يجب أن تكون شهر واحد على الأقل");
        }

        [Fact]
        [Trait("Category", "CreateTenantValidator")]
        public void Validate_WithBasicPlanAndDurationMoreThan12_ShouldFailWithMessage()
        {
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 13;

            var result = _validator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("المدة لا يمكن أن تتجاوز 12 شهراً");
        }
    }
}