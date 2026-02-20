using FluentValidation.TestHelper;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.DTOs.Subscriptions;
using ISP.Application.Validators;

namespace ISP.Tests.Unit.Validators
{
    public class CreateSubscriptionValidatorTest
    {
        private readonly CreateSubscriptionValidator _validator;

        public CreateSubscriptionValidatorTest()
        {
            _validator = new CreateSubscriptionValidator();
        }

        // ============================================
        // الحالات الصحيحة
        // ============================================

        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithValidData_ShouldPass()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = DateTime.UtcNow.Date
            };

            // Act 
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // اختبارات حقل SubscriberId
        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithSubscriberIdZero_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 0,
                PlanId = 1,
                StartDate = DateTime.UtcNow.Date
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SubscriberId)
                .WithErrorMessage("يجب اختيار المشترك");
        }

        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithNegativeSubscriberId_ShouldFail()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = -5,
                PlanId = 1,
                StartDate = DateTime.UtcNow.Date
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SubscriberId);
        }

        // اختبارات حقل PlanId
        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithPlanIdZero_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 0,
                StartDate = DateTime.UtcNow.Date,
            };

            // Act 
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PlanId)
                .WithErrorMessage("يجب اختيار الباقة");
        }

        [Fact]
        public void Validate_WithNegativePlanId_ShouldFail()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = -1,
                StartDate = DateTime.UtcNow.Date
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.PlanId);
        }

        // اختبارات حقل StartDate
        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithYesterdayStartDate_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = DateTime.UtcNow.Date.AddDays(-1)
            };

            // Act 
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.StartDate)
                .WithErrorMessage("تاريخ البدء لا يمكن أن يكون في الماضي");
        }

        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithOldStartDate_ShouldFail()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = new DateTime(2020, 1, 1)
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.StartDate);
        }

        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithEmptyStartDate_ShouldFail()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 1,
                PlanId = 1,
                StartDate = default
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.StartDate);
        }

        // اختبار أخطاء متعددة

        [Fact]
        [Trait("Category", "Validator")]
        public void Validate_WithAllInvalidData_ShouldReturnMultipleErrors()
        {
            // Arrange
            var dto = new CreateSubscriptionDto
            {
                SubscriberId = 0,
                PlanId = 0,
                StartDate = DateTime.UtcNow.Date.AddDays(-10)
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SubscriberId);
            result.ShouldHaveValidationErrorFor(x => x.PlanId);
            result.ShouldHaveValidationErrorFor(x => x.StartDate);
        }
    }
}