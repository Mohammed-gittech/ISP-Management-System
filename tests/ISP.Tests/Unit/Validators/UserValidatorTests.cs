// ============================================
// UserValidatorTests.cs
// Unit tests for CreateUserValidator & UpdateUserValidator
// ============================================

using FluentValidation.TestHelper;
using ISP.Application.DTOs.Users;
using ISP.Application.Validators;

namespace ISP.Tests.Unit.Validators
{
    public class UserValidatorTests
    {
        // ============================================
        // CreateUserValidator Tests
        // ============================================

        private readonly CreateUserValidator _createValidator;
        private readonly UpdateUserValidator _updateValidator;

        public UserValidatorTests()
        {
            _createValidator = new CreateUserValidator();
            _updateValidator = new UpdateUserValidator();
        }

        // ============================================
        // Helper Method — بيانات صحيحة جاهزة
        // ============================================

        // يُنشئ dto صحيح كامل — كل اختبار يعدل الحقل الذي يريد اختباره فقط
        private CreateUserDto CreateValidDto(string role = "TenantAdmin") => new CreateUserDto
        {
            Username = "ahmed_admin",
            Email    = "ahmed@alnoor.com",
            Password = "Admin@123",
            Role     = role,
            TenantId = role == "SuperAdmin" ? null : 1
        };

        // ============================================
        // Valid Data Tests — الحالات الصحيحة
        // ============================================

        // الاختبار الأول: بيانات TenantAdmin صحيحة كاملة
        [Fact]
        public void CreateUser_WithValidTenantAdminData_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto("TenantAdmin");

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // الاختبار الثاني: بيانات Employee صحيحة
        [Fact]
        public void CreateUser_WithValidEmployeeData_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto("Employee");

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // الاختبار الثالث: بيانات SuperAdmin صحيحة — TenantId يجب أن يكون null
        [Fact]
        public void CreateUser_WithValidSuperAdminData_ShouldPass()
        {
            // Arrange
            var dto = CreateValidDto("SuperAdmin");
            dto.TenantId = null; // SuperAdmin لا يحتاج TenantId

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // ============================================
        // Username Tests
        // ============================================

        // الاختبار الرابع: Username فارغ
        [Fact]
        public void CreateUser_WithEmptyUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Username = string.Empty;

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم مطلوب");
        }

        // الاختبار الخامس: Username أقل من 3 أحرف
        [Fact]
        public void CreateUser_WithShortUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Username = "ab"; // حرفان فقط

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        // الاختبار السادس: Username أكثر من 50 حرف
        [Fact]
        public void CreateUser_WithLongUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Username = new string('a', 51); // 51 حرف

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        // ============================================
        // Email Tests
        // ============================================

        // الاختبار السابع: Email فارغ
        [Fact]
        public void CreateUser_WithEmptyEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = string.Empty;

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("البريد الإلكتروني مطلوب");
        }

        // الاختبار الثامن: Email غير صالح
        [Fact]
        public void CreateUser_WithInvalidEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Email = "not-an-email"; // بدون @

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("صيغة البريد الإلكتروني غير صحيحة");
        }

        // ============================================
        // Password Tests
        // ============================================

        // الاختبار التاسع: Password فارغ
        [Fact]
        public void CreateUser_WithEmptyPassword_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Password = string.Empty;

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور مطلوبة");
        }

        // الاختبار العاشر: Password أقل من 6 أحرف
        [Fact]
        public void CreateUser_WithShortPassword_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Password = "123"; // 3 أحرف فقط

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Password)
                .WithErrorMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
        }

        // ============================================
        // Role Tests
        // ============================================

        // الاختبار الحادي عشر: Role فارغ
        [Fact]
        public void CreateUser_WithEmptyRole_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Role = string.Empty;

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Role)
                .WithErrorMessage("الدور مطلوب");
        }

        // الاختبار الثاني عشر: Role غير موجود في القائمة
        [Fact]
        public void CreateUser_WithInvalidRole_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto();
            dto.Role = "Manager"; // دور غير مسموح به

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Role)
                .WithErrorMessage("الدور يجب أن يكون: SuperAdmin, TenantAdmin, أو Employee");
        }

        // الاختبار الثالث عشر: Theory — كل الأدوار الصحيحة تمر
        [Theory]
        [InlineData("SuperAdmin")]
        [InlineData("TenantAdmin")]
        [InlineData("Employee")]
        public void CreateUser_WithValidRole_ShouldPass(string role)
        {
            // Arrange
            var dto = CreateValidDto(role);

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Role);
        }

        // ============================================
        // TenantId Tests
        // ============================================

        // الاختبار الرابع عشر: TenantAdmin بدون TenantId — يجب أن يفشل
        [Fact]
        public void CreateUser_WithTenantAdminAndNullTenantId_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto("TenantAdmin");
            dto.TenantId = null; // TenantAdmin يحتاج TenantId

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("معرف الوكيل مطلوب للأدوار غير SuperAdmin");
        }

        // الاختبار الخامس عشر: SuperAdmin مع TenantId — يجب أن يفشل
        [Fact]
        public void CreateUser_WithSuperAdminAndTenantId_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto("SuperAdmin");
            dto.TenantId = 1; // SuperAdmin لا يحتاج TenantId

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("SuperAdmin لا يحتاج معرف وكيل");
        }

        // الاختبار السادس عشر: Employee بدون TenantId — يجب أن يفشل
        [Fact]
        public void CreateUser_WithEmployeeAndNullTenantId_ShouldFailWithMessage()
        {
            // Arrange
            var dto = CreateValidDto("Employee");
            dto.TenantId = null; // Employee يحتاج TenantId

            // Act
            var result = _createValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.TenantId)
                .WithErrorMessage("معرف الوكيل مطلوب للأدوار غير SuperAdmin");
        }

        // ============================================
        // UpdateUserValidator Tests
        // ============================================

        // الاختبار السابع عشر: بيانات فارغة كاملة — يجب أن تمر (كل الحقول اختيارية)
        [Fact]
        public void UpdateUser_WithAllNullFields_ShouldPass()
        {
            // Arrange
            // UpdateUserDto كل حقوله اختيارية — يمكن إرسال dto فارغ
            var dto = new UpdateUserDto();

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        // الاختبار الثامن عشر: Username صحيح — يجب أن يمر
        [Fact]
        public void UpdateUser_WithValidUsername_ShouldPass()
        {
            // Arrange
            var dto = new UpdateUserDto { Username = "new_admin" };

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Username);
        }

        // الاختبار التاسع عشر: Username أقل من 3 أحرف — يجب أن يفشل
        [Fact]
        public void UpdateUser_WithShortUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new UpdateUserDto { Username = "ab" }; // حرفان فقط

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        // الاختبار العشرون: Username أكثر من 50 حرف — يجب أن يفشل
        [Fact]
        public void UpdateUser_WithLongUsername_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new UpdateUserDto { Username = new string('a', 51) }; // 51 حرف

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Username)
                .WithErrorMessage("اسم المستخدم يجب أن يكون بين 3 و 50 حرفًا");
        }

        // الاختبار الحادي والعشرون: Email صحيح — يجب أن يمر
        [Fact]
        public void UpdateUser_WithValidEmail_ShouldPass()
        {
            // Arrange
            var dto = new UpdateUserDto { Email = "new@alnoor.com" };

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        // الاختبار الثاني والعشرون: Email غير صالح — يجب أن يفشل
        [Fact]
        public void UpdateUser_WithInvalidEmail_ShouldFailWithMessage()
        {
            // Arrange
            var dto = new UpdateUserDto { Email = "not-an-email" }; // بدون @

            // Act
            var result = _updateValidator.TestValidate(dto);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage("صيغة البريد الإلكتروني غير صحيحة");
        }
    }
}