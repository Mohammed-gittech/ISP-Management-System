
using FluentValidation.TestHelper;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.Validators;

namespace ISP.Tests.Unit.Validators
{
    /// <summary>
    /// اختبارات CreateSubscriberValidator
    /// نتأكد أن Validation Rules تعمل بشكل صحيح
    /// </summary>
    public class CreateSubscriberValidatorTests
    {
        private readonly CreateSubscriberValidator _validator;

        public CreateSubscriberValidatorTests()
        {
            _validator = new CreateSubscriberValidator();
        }

        // ============================================
        // TESTS: FullName Validation
        // ============================================

        /// <summary>
        /// الاسم صحيح - يجب أن يمر بنجاح
        /// </summary>
        [Fact]
        public void FullName_Valid_ShouldNotHaveValidationError()
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد علي",
                PhoneNumber = "07801234567"
            };

            // Act 
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(s => s.FullName);
        }

        /// <summary>
        /// الاسم فارغ - يجب أن يفشل
        /// </summary>
        [Fact]
        public void FullName_Empty_ShouldHaveValidationError()
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "",// فارغ!
                PhoneNumber = "07801234567"
            };

            // Act 
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(s => s.FullName);
        }

        /// <summary>
        /// الاسم قصير جداً (أقل من 3 أحرف)
        /// </summary>
        [Fact]
        public void FullName_TooShort_ShouldHaveValidationError()
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أب", // حرفان فقط
                PhoneNumber = "07801234567"
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FullName);
        }

        // ============================================
        // TESTS: PhoneNumber Validation
        // ============================================

        /// <summary>
        /// Theory - اختبار أرقام هواتف عراقية صحيحة
        /// </summary>
        [Theory]
        [InlineData("07801234567")] // زين
        [InlineData("07701234567")] // آسياسيل
        [InlineData("07501234567")] // كورك
        [InlineData("07601234567")] // عراقنا
        [InlineData("07401234567")] // فانوس
        [InlineData("07301234567")] // الخليج
        [InlineData("07901234567")] // أومنيا
        public void PhoneNumber_ValidIraqiNumbers_ShouldNotHaveError(string phoneNumber)
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = phoneNumber
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.PhoneNumber);
        }

        /// <summary>
        /// Theory - اختبار أرقام هواتف غير صحيحة
        /// </summary>
        [Theory]
        [InlineData("")] // فارغ
        [InlineData("123")] // قصير جداً
        [InlineData("07201234567")] // يبدأ بـ 072 (غير موجود)
        [InlineData("07801234")] // ناقص أرقام
        [InlineData("078012345678")] // زيادة أرقام
        [InlineData("01234567890")] // لا يبدأ بـ 07
        public void PhoneNumber_Invalid_ShouldHaveError(string phoneNumber)
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = phoneNumber
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
        }

        // ============================================
        // TESTS: Email Validation
        // ============================================

        /// <summary>
        /// البريد الإلكتروني صحيح
        /// </summary>
        [Theory]
        [InlineData("ahmad@example.com")]
        [InlineData("user.name@company.co.uk")]
        [InlineData("test123@test-mail.com")]
        public void Email_Valid_ShouldNotHaveError(string email)
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567",
                Email = email
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        /// <summary>
        /// البريد الإلكتروني غير صحيح
        /// </summary>
        [Theory]
        [InlineData("invalid-email")] // بدون @
        [InlineData("@example.com")] // بدون username
        [InlineData("user@")] // بدون domain
        public void Email_Invalid_ShouldHaveError(string email)
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567",
                Email = email
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        /// <summary>
        /// Email اختياري - null أو empty يجب أن يمر
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Email_NullOrEmpty_ShouldNotHaveError(string? email)
        {
            // Arrange
            var dto = new CreateSubscriberDto
            {
                FullName = "أحمد محمد",
                PhoneNumber = "07801234567",
                Email = email
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }
    }
}