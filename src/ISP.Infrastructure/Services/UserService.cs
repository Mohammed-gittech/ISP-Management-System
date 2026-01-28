// ============================================
// UserService.cs - تنفيذ خدمة المستخدمين
// ============================================
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
        // 1. GET BY ID
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
        // 2. GET ALL (مع Pagination + Search)
        // ============================================
        public async Task<PagedResultDto<UserDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null)
        {
            // 1. جلب جميع المستخدمين
            var allUsers = await _unitOfWork.Users.GetAllAsync();

            // 2. تطبيق البحث إذا وُجد
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                allUsers = allUsers.Where(u =>
                    u.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // 3. حساب الإجمالي
            var totalCount = allUsers.Count();

            // 4. تطبيق Pagination + Sorting
            var users = allUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5. تحويل إلى DTOs
            var userDtos = _mapper.Map<List<UserDto>>(users);

            // 6. إضافة أسماء الوكلاء
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
        // 3. CREATE USER
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
        // 4. UPDATE USER
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
        // 5. DELETE USER
        // ============================================
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

            await _unitOfWork.Users.DeleteAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("User deleted: {UserId}", id);

            return true;
        }

        // ============================================
        // 6. CHANGE PASSWORD
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
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password changed for user: {UserId}", userId);

            return true;
        }

        // ============================================
        // 7. RESET PASSWORD (Admin only)
        // ============================================
        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            user.PasswordHash = _passwordHasher.HashPassword(newPassword);

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Password reset for user: {UserId}", userId);

            return true;
        }

        // ============================================
        // 8. ASSIGN ROLE
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
        // 9. GET USERS BY TENANT
        // ============================================
        public async Task<PagedResultDto<UserDto>> GetUsersByTenantAsync(int tenantId, int pageNumber, int pageSize)
        {
            // 1. جلب جميع المستخدمين
            var allUsers = await _unitOfWork.Users.GetAllAsync();

            // 2. تصفية حسب TenantId
            var tenantUsers = allUsers.Where(u => u.TenantId == tenantId).ToList();

            // 3. حساب الإجمالي
            var totalCount = tenantUsers.Count;

            // 4. تطبيق Pagination
            var users = tenantUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5. تحويل إلى DTOs
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
        // 10. VALIDATION HELPERS
        // ============================================
        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null)
        {
            var allUsers = await _unitOfWork.Users.GetAllAsync();

            var query = allUsers.Where(u => u.Email == email);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return !query.Any();
        }

        public async Task<bool> IsUsernameUniqueAsync(string username, int? excludeUserId = null)
        {
            var allUsers = await _unitOfWork.Users.GetAllAsync();

            var query = allUsers.Where(u => u.Username == username);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return !query.Any();
        }
    }
}