// ============================================
// LoginRequestValidatorTests.cs
// Unit Tests for LoginRequestValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.DTOs.Auth;
using ISP.Application.Validators;

namespace ISP.Tests.Unit.Validators
{
    public class LoginRequestValidatorTests
    {
        private readonly LoginRequestValidator _validator;

        public LoginRequestValidatorTests()
        {
            _validator = new LoginRequestValidator();
        }

        // ============================================
        // Helper Method — بيانات صحيحة جاهزة
        // ============================================

        private LoginRequestDto CreateValidDto() => new LoginRequestDto
        {
            Email    = "ahmed@alnoor.com",
            Password = "Admin@123"
        };

        // ============================================
        // Valid Data Tests
        // ============================================

        // الاختبار الأول: بيانات صحيحة كاملة — يجب أن تمر
        [Fact]
        public void Login_WithValidData_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto();

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Email Tests
        // ============================================

        // الاختبار الثاني: Email فارغ — يجب أن يفشل
        [Fact]
        public void Login_WithEmptyEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = string.Empty;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("البريد الإلكتروني مطلوب");
        }

        // الاختبار الثالث: Email غير صالح — يجب أن يفشل
        [Fact]
        public void Login_WithInvalidEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = "not-an-email"; // بدون @

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("البريد الإلكتروني غير صالح");
        }

        // ============================================
        // Password Tests
        // ============================================

        // الاختبار الرابع: Password فارغ — يجب أن يفشل
        [Fact]
        public void Login_WithEmptyPassword_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Password = string.Empty;

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور مطلوبة");
        }

        // الاختبار الخامس: Email وPassword فارغان — يجب أن يفشل كلاهما
        [Fact]
        public void Login_WithEmptyEmailAndPassword_ShouldFailBoth()
        {
            // Arrange
            var dto = new LoginRequestDto(); // كل الحقول فارغة

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email);
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }
    }
}