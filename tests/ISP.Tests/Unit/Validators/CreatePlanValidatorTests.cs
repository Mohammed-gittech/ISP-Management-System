using FluentAssertions;
using FluentValidation.TestHelper;
using ISP.Application.DTOs.Plans;
using ISP.Application.Validators;

namespace ISP.Tests.Unit.Validators
{
    public class CreatePlanValidatorTests
    {
        private readonly CreatePlanValidator _validator;

        public CreatePlanValidatorTests()
        {
            _validator = new CreatePlanValidator();
        }

        // TESTS: Name Validation
        [Fact]
        public void Name_Valid_SouldNotHaveError()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة 50 ميجا",
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Name);
        }

        [Fact]
        public void Name_Empty_ShouldHaveError()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "",
                Speed = 50,
                Price = 15000,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name).WithErrorMessage("اسم الباقة مطلوب");
        }

        [Fact]
        public void Name_TooLong_ShouldHaveError()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = new string('ا', 51),
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Name_ExactlyFiftyCharacters_ShouldNotHaveError()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = new string('ا', 50),
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        // TESTS: Speed Validation

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)] // الحد الأقصى
        public void Speed_ValidValue_ShouldNotHaveError(int speed)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = speed,
                Price = 15000,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Speed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10)]
        public void Speed_ZeroOrNegative_ShouldHaveError(int speed)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = speed,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result
                .ShouldHaveValidationErrorFor(x => x.Speed)
                .WithErrorMessage("السرعة يجب أن تكون أكبر من صفر");
        }

        [Theory]
        [InlineData(1001)]
        [InlineData(2000)]
        [InlineData(10000)]
        public void Speed_ExceedsMaximum_ShouldHaveError(int speed)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = speed,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result
                .ShouldHaveValidationErrorFor(x => x.Speed)
                .WithErrorMessage("السرعة يجب أن لا تتجاوز 1000 Mbps");
        }

        // TESTS: Price Validation

        [Theory]
        [InlineData(1)]
        [InlineData(1000)]
        [InlineData(15000)]
        [InlineData(50000)]
        [InlineData(100000)]
        public void Price_ValidValues_ShouldNotHaveError(decimal price)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = 50,
                Price = price,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Price);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-1000)]
        public void Price_ZeroOrNegative_ShouldHaveError(decimal price)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = 50,
                Price = price,
                DurationDays = 30,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result
                .ShouldHaveValidationErrorFor(x => x.Price)
                .WithErrorMessage("السعر يجب أن يكون أكبر من صفر");
        }

        // TESTS: DurationDays Validation
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(30)]
        [InlineData(90)]
        [InlineData(365)] // الحد الأقصى
        public void DurationDays_ValidValues_ShouldNotHaveError(int days)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = 50,
                Price = 15000,
                DurationDays = days,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.DurationDays);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-30)]
        public void DurationDays_ZeroOrNegative_ShouldHaveError(int days)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = 50,
                Price = 15000,
                DurationDays = days,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result
                .ShouldHaveValidationErrorFor(x => x.DurationDays)
                .WithErrorMessage("المدة يجب أن تكون أكبر من صفر");
        }

        [Theory]
        [InlineData(366)]
        [InlineData(500)]
        [InlineData(1000)]
        public void DurationDays_ExceedsMaximum_ShouldHaveError(int days)
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة",
                Speed = 50,
                Price = 15000,
                DurationDays = days,
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result
                .ShouldHaveValidationErrorFor(x => x.DurationDays)
                .WithErrorMessage("المدة يجب أن لا تتجاوز 365 يوم");
        }

        // TESTS: Complete DTO Validation
        [Fact]
        public void CompleteDto_AllValid_NoErrors()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "باقة 100 ميجا - شهرية",
                Speed = 100,
                Price = 25000,
                DurationDays = 30,
                Description = "باقة شهرية سرعة 100 ميجا",
            };
            // Act
            var result = _validator.TestValidate(dto);
            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void CompleteDto_MultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var dto = new CreatePlanDto
            {
                Name = "",
                Speed = 0,
                Price = -100,
                DurationDays = 400,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Errors.Should().HaveCount(4); // 4 أخطاء

            result.ShouldHaveValidationErrorFor(x => x.Name);
            result.ShouldHaveValidationErrorFor(x => x.Speed);
            result.ShouldHaveValidationErrorFor(x => x.Price);
            result.ShouldHaveValidationErrorFor(x => x.DurationDays);
        }

        // TESTS: Real-World Scenarios
        [Fact]
        public void RealWorld_BasicHomePlan_Valid()
        {
            // Arrange - باقة منزلية أساسية
            var dto = new CreatePlanDto
            {
                Name = "باقة المنزل الأساسية",
                Speed = 50,
                Price = 15000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void RealWorld_BusinessPlan_Valid()
        {
            // Arrange - باقة تجارية
            var dto = new CreatePlanDto
            {
                Name = "باقة الأعمال - 500 ميجا",
                Speed = 500,
                Price = 80000,
                DurationDays = 30,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void RealWorld_YearlyPlan_Valid()
        {
            // Arrange - باقة سنوية
            var dto = new CreatePlanDto
            {
                Name = "باقة 100 ميجا - سنوية",
                Speed = 100,
                Price = 250000,
                DurationDays = 365,
            };

            // Act
            var result = _validator.TestValidate(dto);

            // Assert
            result.IsValid.Should().BeTrue();
        }
    }
}
