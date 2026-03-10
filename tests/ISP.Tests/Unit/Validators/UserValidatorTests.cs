// ============================================
// UserValidatorTests.cs
// Unit Tests for CreateUserValidator & UpdateUserValidator
// ============================================

using FluentAssertions;
using FluentValidation.TestHelper;
using ISP.Application.DTOs.Users;
using ISP.Application.Validators;
using Microsoft.Extensions.Configuration;

namespace ISP.Tests.Unit.Validators
{
    public class UserValidatorTests
    {
        private readonly CreateUserValidator _createValidator;
        private readonly UpdateUserValidator _updateValidator;

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

        public UserValidatorTests()
        {
            // ← IConfiguration يُمرَّر للـ Validators التي تحتاجه
            _createValidator = new CreateUserValidator(BuildConfig());
            _updateValidator = new UpdateUserValidator();
        }

        // ============================================
        // Helper Method — بيانات صحيحة جاهزة
        // ============================================

        private CreateUserDto CreateValidDto(string role = "TenantAdmin") => new CreateUserDto
        {
            Username = "ahmed_admin",
            Email = "ahmed@alnoor.com",
            Password = "Admin@123",
            // ← "Admin@123" تمر الآن: 8+ أحرف، Uppercase، Lowercase، Digit، Special
            Role = role,
            TenantId = role == "SuperAdmin" ? null : 1
        };

        // ============================================
        // Valid Data Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithValidTenantAdminData_ShouldPass()
        {
            var result = _createValidator.TestValidate(CreateValidDto("TenantAdmin"));
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithValidEmployeeData_ShouldPass()
        {
            var result = _createValidator.TestValidate(CreateValidDto("Employee"));
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithValidSuperAdminData_ShouldPass()
        {
            var dto = CreateValidDto("SuperAdmin");
            dto.TenantId = null;
            var result = _createValidator.TestValidate(dto);
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Username Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithEmptyUsername_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Username = string.Empty;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم مطلوب");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithShortUsername_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Username = "ab";

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithLongUsername_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Username = new string('a', 51);

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        // ============================================
        // Email Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithEmptyEmail_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Email = string.Empty;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("البريد الإلكتروني مطلوب");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithInvalidEmail_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Email = "not-an-email";

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("صيغة البريد الإلكتروني غير صحيحة");
        }

        // ============================================
        // Password Tests ← محدَّث بالكامل
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithEmptyPassword_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Password = string.Empty;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور مطلوبة");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithPasswordShorterThan8Chars_ShouldFail()
        {
            // ← تعديل: من 6 إلى 8 حسب الـ Policy الجديدة
            var dto = CreateValidDto();
            dto.Password = "Aa1!aaa"; // 7 أحرف فقط

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تكون على الأقل 8 حرفاً");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithPasswordWithoutUppercase_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.Password = "admin@123"; // بدون Uppercase

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف كبير على الأقل (A-Z)");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithPasswordWithoutLowercase_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.Password = "ADMIN@123"; // بدون Lowercase

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على حرف صغير على الأقل (a-z)");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithPasswordWithoutDigit_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.Password = "Admin@abc"; // بدون رقم

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رقم واحد على الأقل (0-9)");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithPasswordWithoutSpecialChar_ShouldFail()
        {
            var dto = CreateValidDto();
            dto.Password = "Admin1234"; // بدون رمز خاص

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تحتوي على رمز خاص على الأقل (!@#$%^&*)");
        }

        // ============================================
        // Role Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithEmptyRole_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Role = string.Empty;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Role)
                .WithErrorMessage("الدور مطلوب");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithInvalidRole_ShouldFailWithMessage()
        {
            var dto = CreateValidDto();
            dto.Role = "Manager";

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.Role)
                .WithErrorMessage("الدور يجب أن يكون: SuperAdmin, TenantAdmin, أو Employee");
        }

        [Theory]
        [Trait("Category", "CreateUserValidator")]
        [InlineData("SuperAdmin")]
        [InlineData("TenantAdmin")]
        [InlineData("Employee")]
        public void CreateUser_WithValidRole_ShouldPass(string role)
        {
            var result = _createValidator.TestValidate(CreateValidDto(role));
            result.ShouldNotHaveValidationErrorFor(x => x.Role);
        }

        // ============================================
        // TenantId Tests
        // ============================================

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithTenantAdminAndNullTenantId_ShouldFailWithMessage()
        {
            var dto = CreateValidDto("TenantAdmin");
            dto.TenantId = null;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("معرف الوكيل مطلوب للأدوار غير SuperAdmin");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithSuperAdminAndTenantId_ShouldFailWithMessage()
        {
            var dto = CreateValidDto("SuperAdmin");
            dto.TenantId = 1;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("SuperAdmin لا يحتاج معرف وكيل");
        }

        [Fact]
        [Trait("Category", "CreateUserValidator")]
        public void CreateUser_WithEmployeeAndNullTenantId_ShouldFailWithMessage()
        {
            var dto = CreateValidDto("Employee");
            dto.TenantId = null;

            var result = _createValidator.TestValidate(dto);

            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("معرف الوكيل مطلوب للأدوار غير SuperAdmin");
        }

        // ============================================
        // UpdateUserValidator Tests
        // ============================================

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithAllNullFields_ShouldPass()
        {
            var result = _updateValidator.TestValidate(new UpdateUserDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithValidUsername_ShouldPass()
        {
            var result = _updateValidator.TestValidate(new UpdateUserDto { Username = "new_admin" });
            result.ShouldNotHaveValidationErrorFor(x => x.Username);
        }

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithShortUsername_ShouldFailWithMessage()
        {
            var result = _updateValidator.TestValidate(new UpdateUserDto { Username = "ab" });
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithLongUsername_ShouldFailWithMessage()
        {
            var result = _updateValidator.TestValidate(
                new UpdateUserDto { Username = new string('a', 51) });
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithValidEmail_ShouldPass()
        {
            var result = _updateValidator.TestValidate(
                new UpdateUserDto { Email = "new@alnoor.com" });
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        [Trait("Category", "UpdateUserValidator")]
        public void UpdateUser_WithInvalidEmail_ShouldFailWithMessage()
        {
            var result = _updateValidator.TestValidate(
                new UpdateUserDto { Email = "not-an-email" });
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("صيغة البريد الإلكتروني غير صحيحة");
        }
    }
}