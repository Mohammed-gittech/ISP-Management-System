using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Users;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة المستخدمين
    /// ✅ Soft Delete Support
    /// ⚠️ حذف Users أكثر حساسية من Entities الأخرى
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ICurrentTenantService _currentTenantService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPasswordHasher passwordHasher,
            ICurrentTenantService currentTenantService,
            ILogger<UserService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
            _currentTenantService = currentTenantService;
            _logger = logger;
        }

        // ============================================
        // GET BY ID
        // ============================================
        public async Task<UserDto?> GetByIdAsync(int id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null) return null;

            var dto = _mapper.Map<UserDto>(user);

            // إضافة اسم الوكيل إذا كان موجود
            if (user.TenantId.HasValue)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value);
                dto.TenantName = tenant?.Name ?? "";
            }

            return dto;
        }

        // ============================================
        // GET ALL (مع Pagination + Search)
        // ============================================
        public async Task<PagedResultDto<UserDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            IEnumerable<User> allUsers;

            // تطبيق البحث إذا وُجد
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                allUsers = await _unitOfWork.Users.GetAllAsync(u =>
                    u.Username.Contains(searchTerm) || u.Email.Contains(searchTerm));
            }
            else
            {
                allUsers = await _unitOfWork.Users.GetAllAsync();
            }

            // حساب الإجمالي
            var totalCount = allUsers.Count();

            // تطبيق Pagination + Sorting
            var users = allUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // تحويل إلى DTOs
            var userDtos = _mapper.Map<List<UserDto>>(users);

            // إضافة أسماء الوكلاء
            foreach (var dto in userDtos)
            {
                if (dto.TenantId.HasValue)
                {
                    var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId.Value);
                    dto.TenantName = tenant?.Name ?? "";
                }
            }

            return new PagedResultDto<UserDto>
            {
                Items = userDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // CREATE USER
        // ============================================
        public async Task<UserDto> CreateAsync(CreateUserDto dto)
        {
            // 1. التحقق من تفرّد Email و Username
            if (!await IsEmailUniqueAsync(dto.Email))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقًا");

            if (!await IsUsernameUniqueAsync(dto.Username))
                throw new InvalidOperationException("اسم المستخدم مستخدم مسبقًا");

            // 2. تحويل Role من String إلى Enum
            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                throw new InvalidOperationException("الدور غير صحيح");

            // 3. Hash Password
            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // 4. إنشاء Entity
            var user = new User
            {
                TenantId = dto.TenantId,
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = passwordHash,
                Role = roleEnum,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // 5. حفظ في Database
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User created: {UserId} - {Username}", user.Id, user.Username);

            return await GetByIdAsync(user.Id) ?? throw new Exception("فشل إنشاء المستخدم");
        }

        // ============================================
        // UPDATE USER
        // ============================================
        public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null) return null;

            // تحديث الحقول المرسلة فقط
            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                if (!await IsUsernameUniqueAsync(dto.Username, id))
                    throw new InvalidOperationException("اسم المستخدم مستخدم مسبقًا");
                user.Username = dto.Username;
            }

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                if (!await IsEmailUniqueAsync(dto.Email, id))
                    throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقًا");
                user.Email = dto.Email;
            }

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User updated: {UserId}", id);

            return await GetByIdAsync(id);
        }

        // ============================================
        // SOFT DELETE (محدث - حذر!)
        // ============================================

        /// <summary>
        /// حذف ناعم لمستخدم
        /// ⚠️ حذف مستخدم قد يسبب مشاكل في Authentication/Authorization
        /// ✅ يُوصى بـ Deactivate (IsActive = false) بدلاً من Delete
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null) return false;

            // منع حذف SuperAdmin الوحيد
            if (user.Role == UserRole.SuperAdmin)
            {
                var allUsers = await _unitOfWork.Users.GetAllAsync();
                var superAdminCount = allUsers.Count(u => u.Role == UserRole.SuperAdmin);

                if (superAdminCount <= 1)
                    throw new InvalidOperationException("لا يمكن حذف آخر SuperAdmin");
            }

            // منع حذف المستخدم الحالي
            if (_currentTenantService.UserId.HasValue && _currentTenantService.UserId == id)
            {
                throw new InvalidOperationException("لا يمكنك حذف نفسك");
            }

            _logger.LogWarning("Soft deleting User {UserId} - {Username}", id, user.Username);

            // قبل الحذف Refresh Tokens ← جديد: إلغاء كل   
            // المستخدم المحذوف لا يجب أن يظل مسجّل الدخول
            await RevokeAllUserTokensAsync(id, "User soft deleted");

            await _unitOfWork.Users.SoftDeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User {UserId} soft deleted successfully", id);

            return true;
        }

        // ============================================
        // RESTORE 
        // ============================================

        public async Task<bool> RestoreAsync(int id)
        {
            _logger.LogInformation("Attempting to restore User {UserId}", id);

            // التحقق من عدم تكرار Email/Username بعد الاسترجاع
            var user = await _unitOfWork.Users.GetByIdIncludingDeletedAsync(id);

            if (user == null || !user.IsDeleted)
                return false;

            // التحقق من تفرّد Email/Username (قبل الاسترجاع)
            var existingEmail = await _unitOfWork.Users.GetAllAsync(u => u.Email == user.Email && u.TenantId == user.TenantId);
            if (existingEmail.Any())
            {
                throw new InvalidOperationException(
                    $"لا يمكن الاسترجاع. البريد الإلكتروني {user.Email} مستخدم من قبل مستخدم آخر");
            }

            var existingUsername = await _unitOfWork.Users.GetAllAsync(u => u.Username == user.Username);
            if (existingUsername.Any())
            {
                throw new InvalidOperationException(
                    $"لا يمكن الاسترجاع. اسم المستخدم {user.Username} مستخدم من قبل مستخدم آخر");
            }

            var restored = await _unitOfWork.Users.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("User {UserId} restored successfully", id);
            }

            return restored;
        }

        // ============================================
        // GET DELETED 
        // ============================================

        public async Task<PagedResultDto<UserDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10)
        {
            var deleted = await _unitOfWork.Users.GetDeletedAsync();

            var totalCount = deleted.Count();
            var items = deleted
                .OrderByDescending(u => u.DeletedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userDtos = _mapper.Map<List<UserDto>>(items);

            // إضافة أسماء الوكلاء
            foreach (var dto in userDtos)
            {
                if (dto.TenantId.HasValue)
                {
                    var tenant = await _unitOfWork.Tenants.GetByIdAsync(dto.TenantId.Value);
                    dto.TenantName = tenant?.Name ?? "";
                }
            }

            return new PagedResultDto<UserDto>
            {
                Items = userDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // PERMANENT DELETE ( SuperAdmin only)
        // ============================================

        public async Task<bool> PermanentDeleteAsync(int id)
        {
            _logger.LogCritical("PERMANENT DELETE requested for User {UserId}", id);

            var user = await _unitOfWork.Users.GetByIdIncludingDeletedAsync(id);

            if (user == null)
                return false;

            if (!user.IsDeleted)
            {
                throw new InvalidOperationException("لا يمكن الحذف النهائي لمستخدم نشط. استخدم Soft Delete أولاً");
            }

            // منع حذف SuperAdmin حتى لو محذوف soft
            if (user.Role == UserRole.SuperAdmin)
            {
                throw new InvalidOperationException("لا يمكن الحذف النهائي لحساب SuperAdmin");
            }

            await _unitOfWork.Users.DeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogCritical("User {UserId} - {Username} PERMANENTLY DELETED", id, user.Username);

            return true;
        }

        // ============================================
        // PASSWORD OPERATIONS
        // ============================================

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            // 1. التحقق من كلمة المرور القديمة
            if (!_passwordHasher.VerifyPassword(dto.OldPassword, user.PasswordHash))
                throw new InvalidOperationException("كلمة المرور القديمة غير صحيحة");

            // 2. Hash كلمة المرور الجديدة
            user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);

            await _unitOfWork.Users.UpdateAsync(user);

            // 3. Refresh Tokens  إلغاء كل 
            // كلمة المرور تغيّرت → كل الأجهزة يجب أن تعيد تسجيل الدخول
            // قديم Refresh Token يطرد أي مهاجم يملك  
            await RevokeAllUserTokensAsync(userId, "Password changed by user");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password changed for user: {UserId}", userId);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(int userId, ResetPasswordDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);

            await _unitOfWork.Users.UpdateAsync(user);

            // Refresh Tokens إلغاء كل 
            // غيّر كلمة المرور → يجب طرد كل الجلسات فوراً Admin
            // قديم Refresh Token يمنع المهاجم من الاستمرار باستخدام 
            await RevokeAllUserTokensAsync(userId, "Password reset by admin");

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password reset for user: {UserId}", userId);

            return true;
        }

        // ============================================
        // ASSIGN ROLE
        // ============================================

        public async Task<bool> AssignRoleAsync(int userId, string role)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            if (!Enum.TryParse<UserRole>(role, out var roleEnum))
                throw new InvalidOperationException("الدور غير صحيح");

            user.Role = roleEnum;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Role assigned: {UserId} -> {Role}", userId, role);

            return true;
        }

        // ============================================
        // GET USERS BY TENANT
        // ============================================

        public async Task<PagedResultDto<UserDto>> GetUsersByTenantAsync(int tenantId, int pageNumber, int pageSize)
        {
            var tenantUsers = await _unitOfWork.Users.GetByTenantAsync(tenantId);

            var totalCount = tenantUsers.Count();

            var users = tenantUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var userDtos = _mapper.Map<List<UserDto>>(users);

            return new PagedResultDto<UserDto>
            {
                Items = userDtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // VALIDATION HELPERS
        // ============================================

        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null)
        {
            var users = await _unitOfWork.Users.GetAllAsync(u => u.Email == email);

            if (excludeUserId.HasValue)
                users = users.Where(u => u.Id != excludeUserId.Value);

            return !users.Any();
        }

        public async Task<bool> IsUsernameUniqueAsync(string username, int? excludeUserId = null)
        {
            var users = await _unitOfWork.Users.GetAllAsync(u => u.Username == username);

            if (excludeUserId.HasValue)
                users = users.Where(u => u.Id != excludeUserId.Value);

            return !users.Any();
        }

        // ============================================
        // PRIVATE HELPERS
        // ============================================

        /// <summary>
        /// النشطة للمستخدم Refresh Tokens إلغاء كل 
        /// يُستدعى عند: تغيير كلمة المرور، إعادة تعيينها، حذف المستخدم
        /// </summary>
        /// <param name="userId">معرّف المستخدم</param>
        /// <param name="reason"> Logging سبب الإلغاء </param>
        private async Task RevokeAllUserTokensAsync(int userId, string reason)
        {
            // Get All Refresh Tokens for the user that are not revoked
            var activeTokens = await _unitOfWork.RefreshTokens.GetAllAsync(
                t => t.UserId == userId && !t.IsRevoked);

            if (!activeTokens.Any())
                return;

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.RefreshTokens.UpdateAsync(token);
            }

            _logger.LogInformation(
                "Revoked {Count} refresh tokens for user {UserId}. Reason: {Reason}",
                activeTokens.Count(), userId, reason);

            // هنا SaveChangesAsync ملاحظة: لا 
            //(ChangePasswordAsync / ResetPasswordAsync / DeleteAsync) المُستدعي 
            // في النهاية SaveChangesAsync هو من يستدعي
            // واحدة Transaction هكذا يتم حفظ كل شيء في 
        }
    }
}