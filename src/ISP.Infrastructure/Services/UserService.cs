using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Users;
using ISP.Application.Helpers;
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
            // 1. Check email uniqueness
            if (!await IsEmailUniqueAsync(dto.Email))
                throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقًا");

            // 2. Check username uniqueness
            if (!await IsUsernameUniqueAsync(dto.Username))
                throw new InvalidOperationException("اسم المستخدم مستخدم مسبقًا");

            // 3. Parse role
            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                throw new InvalidOperationException("الدور غير صحيح");

            // 3. Hash Password
            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // 5. Create entity
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

            // 6. Save to database
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // User created successfully
            _logger.LogInformation(
                "User created successfully | User:{UserId} | Username:{Username} | Tenant:{TenantId} | Role:{Role}",
                user.Id, user.Username, user.TenantId, roleEnum);

            return await GetByIdAsync(user.Id) ?? throw new Exception("فشل إنشاء المستخدم");
        }

        // ============================================
        // UPDATE USER
        // ============================================
        public async Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null) return null;

            // Track what changed for logging
            var changes = new List<string>();

            // Update username if provided
            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                if (!await IsUsernameUniqueAsync(dto.Username, id))
                    throw new InvalidOperationException("اسم المستخدم مستخدم مسبقًا");

                changes.Add($"Username:{user.Username}→{dto.Username}");

                user.Username = dto.Username;
            }

            // Update email if provided
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                if (!await IsEmailUniqueAsync(dto.Email, id))
                    throw new InvalidOperationException("البريد الإلكتروني مستخدم مسبقًا");

                changes.Add($"Email:{EmailHelper.Mask(user.Email)}→{EmailHelper.Mask(dto.Email)}");

                user.Email = dto.Email;
            }

            // Update active status if provided
            if (dto.IsActive.HasValue)
            {
                changes.Add($"IsActive:{user.IsActive}→{dto.IsActive.Value}");
                user.IsActive = dto.IsActive.Value;
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // User updated successfully
            _logger.LogInformation(
                "User updated successfully | User:{UserId} | Tenant:{TenantId} | Changes:{Changes}",
                id, user.TenantId, string.Join(", ", changes));

            return await GetByIdAsync(id);
        }

        // ============================================
        // SOFT DELETE 
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

            // Prevent deleting last SuperAdmin
            if (user.Role == UserRole.SuperAdmin)
            {
                var allUsers = await _unitOfWork.Users.GetAllAsync();
                var superAdminCount = allUsers.Count(u => u.Role == UserRole.SuperAdmin);

                if (superAdminCount <= 1)
                {
                    _logger.LogWarning(
                        "Attempt to delete last SuperAdmin blocked | User:{UserId}",
                        id);

                    throw new InvalidOperationException("لا يمكن حذف آخر SuperAdmin");
                }
            }

            // Prevent self-deletion
            if (_currentTenantService.UserId.HasValue && _currentTenantService.UserId == id)
            {
                _logger.LogWarning(
                    "Self-deletion attempt blocked | User:{UserId}",
                    id);

                throw new InvalidOperationException("لا يمكنك حذف نفسك");
            }

            // Revoke all active tokens before soft delete
            await RevokeAllUserTokensAsync(id, "User soft deleted");

            await _unitOfWork.Users.SoftDeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // User soft deleted successfully
            _logger.LogWarning(
                "User soft deleted | User:{UserId} | Username:{Username} | Tenant:{TenantId} | Role:{Role}",
                id, user.Username, user.TenantId, user.Role);

            return true;
        }

        // ============================================
        // RESTORE 
        // ============================================
        public async Task<bool> RestoreAsync(int id)
        {
            var user = await _unitOfWork.Users.GetByIdIncludingDeletedAsync(id);

            // User not found or not deleted
            if (user == null || !user.IsDeleted)
            {
                _logger.LogWarning(
                    "Restore attempt on non-deleted or missing user | User:{UserId}",
                    id);

                return false;
            }

            // Check email uniqueness before restore
            var existingEmail = await _unitOfWork.Users.GetAllAsync(
                u => u.Email == user.Email && u.TenantId == user.TenantId);

            if (existingEmail.Any())
            {
                _logger.LogWarning(
                    "Restore blocked — email conflict | User:{UserId} | Email:{Email}",
                    id, EmailHelper.Mask(user.Email));

                throw new InvalidOperationException(
                    $"لا يمكن الاسترجاع. البريد الإلكتروني {user.Email} مستخدم من قبل مستخدم آخر");
            }

            // Check username uniqueness before restore
            var existingUsername = await _unitOfWork.Users.GetAllAsync(
                u => u.Username == user.Username);

            if (existingUsername.Any())
            {
                _logger.LogWarning(
                    "Restore blocked — username conflict | User:{UserId} | Username:{Username}",
                    id, user.Username);

                throw new InvalidOperationException(
                    $"لا يمكن الاسترجاع. اسم المستخدم {user.Username} مستخدم من قبل مستخدم آخر");
            }

            var restored = await _unitOfWork.Users.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();

                // User restored successfully
                _logger.LogInformation(
                    "User restored successfully | User:{UserId} | Username:{Username} | Tenant:{TenantId}",
                    id, user.Username, user.TenantId);
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
            var user = await _unitOfWork.Users.GetByIdIncludingDeletedAsync(id);

            if (user == null)
                return false;

            // Prevent permanent delete of active user
            if (!user.IsDeleted)
            {
                _logger.LogWarning(
                    "Permanent delete attempt on active user blocked | User:{UserId}",
                    id);

                throw new InvalidOperationException("لا يمكن الحذف النهائي لمستخدم نشط. استخدم Soft Delete أولاً");
            }

            // Prevent permanent delete of SuperAdmin
            if (user.Role == UserRole.SuperAdmin)
            {
                _logger.LogWarning(
                    "Permanent delete attempt on SuperAdmin blocked | User:{UserId}",
                    id);
                throw new InvalidOperationException("لا يمكن الحذف النهائي لحساب SuperAdmin");
            }

            await _unitOfWork.Users.DeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Critical — permanent delete cannot be undone
            _logger.LogCritical(
                "User PERMANENTLY DELETED | User:{UserId} | Username:{Username} | Tenant:{TenantId} | Role:{Role}",
                id, user.Username, user.TenantId, user.Role);

            return true;
        }

        // ============================================
        // PASSWORD OPERATIONS
        // ============================================
        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            // Verify old password
            if (!_passwordHasher.VerifyPassword(dto.OldPassword, user.PasswordHash))
            {
                _logger.LogWarning(
                    "Failed password change attempt — wrong old password | User:{UserId}",
                    userId);

                throw new InvalidOperationException("كلمة المرور القديمة غير صحيحة");
            }

            // Hash new password
            user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);

            await _unitOfWork.Users.UpdateAsync(user);

            // Revoke all tokens — force re-login on all devices
            await RevokeAllUserTokensAsync(userId, "Password changed by user");

            await _unitOfWork.SaveChangesAsync();

            // Password changed successfully
            _logger.LogInformation(
                    "Password changed successfully | User:{UserId} | Tenant:{TenantId}",
                    userId, user.TenantId);

            return true;
        }

        public async Task<bool> ResetPasswordAsync(int userId, ResetPasswordDto dto)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            // Hash new password
            user.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);

            await _unitOfWork.Users.UpdateAsync(user);

            // Revoke all tokens — force re-login on all devices
            await RevokeAllUserTokensAsync(userId, "Password reset by admin");

            await _unitOfWork.SaveChangesAsync();

            // Password reset successfully
            _logger.LogWarning(
                "Password reset by admin | User:{UserId} | Tenant:{TenantId} | Admin:{AdminId}",
                userId, user.TenantId, _currentTenantService.UserId);

            return true;
        }

        // ============================================
        // ASSIGN ROLE
        // ============================================
        public async Task<bool> AssignRoleAsync(int userId, string role)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            // Parse new role
            if (!Enum.TryParse<UserRole>(role, out var roleEnum))
            {
                _logger.LogWarning(
                    "Invalid role assignment attempt | User:{UserId} | Role:{Role}",
                    userId, role);

                throw new InvalidOperationException("الدور غير صحيح");
            }
            // Track old role for logging
            var oldRole = user.Role;

            user.Role = roleEnum;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
            // Role assigned successfully
            _logger.LogWarning(
                "Role changed | User:{UserId} | Tenant:{TenantId} | Role:{OldRole}→{NewRole} | Admin:{AdminId}",
                userId, user.TenantId, oldRole, roleEnum, _currentTenantService.UserId);

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