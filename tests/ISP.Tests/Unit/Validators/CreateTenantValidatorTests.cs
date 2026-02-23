// ============================================
// CreateTenantValidatorTests.cs
// Unit tests for CreateTenantValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Validators;
using ISP.Domain.Enums;

namespace ISP.Tests.Unit.Validators
{
    public class CreateTenantValidatorTests
    {
        private readonly CreateTenantValidator _validator;

        public CreateTenantValidatorTests()
        {
            _validator = new CreateTenantValidator();
        }

        // ============================================
        // Helper Method — بيانات صحيحة جاهزة
        // ============================================

        // يُنشئ dto صحيح كامل — كل اختبار يعدل الحقل الذي يريد اختباره فقط
        private CreateTenantDto CreateValidDto(TenantPlan plan = TenantPlan.Free) => new CreateTenantDto
        {
            Name = "شركة النور",
            ContactEmail = "info@alnoor.com",
            ContactPhone = "0501234567",
            SubscriptionPlan = plan,
            DurationMonths = 1,
            AdminUsername = "admin",
            AdminEmail = "admin@alnoor.com",
            AdminPassword = "Admin@123"
        };

        // ============================================
        // Valid Data Tests — الحالات الصحيحة
        // ============================================

        // الاختبار الأول: Free Plan بيانات صحيحة كاملة
        [Fact]
        public void Validate_WithValidFreePlanData_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Free);

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // الاختبار الثاني: Basic Plan مع 3 أشهر
        [Fact]
        public void Validate_WithValidBasicPlan3Months_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 3;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // الاختبار الثالث: Pro Plan مع 12 شهر
        [Fact]
        public void Validate_WithValidProPlan12Months_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Pro);
            dto.DurationMonths = 12;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Name Tests
        // ============================================

        // الاختبار الرابع: Name فارغ
        [Fact]
        public void Validate_WithEmptyName_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Name = string.Empty;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorMessage("اسم الشركة مطلوب");
        }

        // الاختبار الخامس: Name أكثر من 100 حرف
        [Fact]
        public void Validate_WithNameExceeding100Characters_ShouldFail()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Name = new string('أ', 101); // 101 حرف

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorMessage("اسم الشركة لا يمكن أن يتجاوز 100 حرف");
        }

        // ============================================
        // ContactEmail Tests
        // ============================================

        // الاختبار السادس: Email فارغ
        [Fact]
        public void Validate_WithEmptyContactEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.ContactEmail = string.Empty;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ContactEmail)
                .WithErrorMessage("البريد الإلكتروني مطلوب");
        }

        // الاختبار السابع: Email غير صالح
        [Fact]
        public void Validate_WithInvalidContactEmail_ShouldFail()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.ContactEmail = "not-an-email"; // بدون @

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.ContactEmail)
                .WithErrorMessage("البريد الإلكتروني غير صالح");
        }

        // ============================================
        // AdminUsername Tests
        // ============================================

        // الاختبار الثامن: AdminUsername أقل من 3 أحرف
        [Fact]
        public void Validate_WithShortAdminUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.AdminUsername = "ab"; // حرفان فقط

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AdminUsername)
                .WithErrorMessage("اسم المستخدم يجب أن يكون 3 أحرف على الأقل");
        }

        // ============================================
        // AdminPassword Tests
        // ============================================

        // الاختبار التاسع: AdminPassword أقل من 6 أحرف
        [Fact]
        public void Validate_WithShortAdminPassword_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.AdminPassword = "123"; // 3 أحرف فقط

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.AdminPassword)
                .WithErrorMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
        }

        // ============================================
        // DurationMonths Tests
        // ============================================

        // الاختبار العاشر: Free Plan مع DurationMonths = 1 — يجب أن يمر
        [Fact]
        public void Validate_WithFreePlanAndDuration1_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Free);
            dto.DurationMonths = 1;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DurationMonths);
        }

        // الاختبار الحادي عشر: Free Plan مع DurationMonths = 6 — يجب أن يفشل
        [Fact]
        public void Validate_WithFreePlanAndDurationMoreThan1_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Free);
            dto.DurationMonths = 6; // Free يقبل شهر واحد فقط

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("الباقة المجانية شهر واحد فقط");
        }

        // الاختبار الثاني عشر: Basic Plan مع DurationMonths = 0 — يجب أن يفشل
        [Fact]
        public void Validate_WithBasicPlanAndDuration0_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 0;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("المدة يجب أن تكون شهر واحد على الأقل");
        }

        // الاختبار الثالث عشر: Basic Plan مع DurationMonths = 13 — يجب أن يفشل
        [Fact]
        public void Validate_WithBasicPlanAndDurationMoreThan12_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto(TenantPlan.Basic);
            dto.DurationMonths = 13; // أكثر من الحد الأقصى

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.DurationMonths)
                .WithErrorMessage("المدة لا يمكن أن تتجاوز 12 شهراً");
        }
    }
}