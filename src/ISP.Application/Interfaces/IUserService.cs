// ============================================
// IUserService.cs - واجهة خدمة المستخدمين
// ===========================================
using System.Reflection.Metadata;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Users;

namespace ISP.Application.Interfaces
{
    public interface IUserService
    {
        // CRUD Operations
        Task<UserDto?> GetByIdAsync(int id);
        Task<PagedResultDto<UserDto>> GetAllAsync(int pageNumber, int pageSize, string? searchTerm = null);
        Task<UserDto> CreateAsync(CreateUserDto dto);
        Task<UserDto?> UpdateAsync(int id, UpdateUserDto dto);

        // Password Management
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);
        Task<bool> ResetPasswordAsync(int userId, string newPassword);

        // Role Management
        Task<bool> AssignRoleAsync(int userId, string role);

        // Tenant-specific
        Task<PagedResultDto<UserDto>> GetUsersByTenantAsync(int tenantId, int pageNumber, int pageSize);

        // Validation
        Task<bool> IsEmailUniqueAsync(string email, int? excludeUserId = null);
        Task<bool> IsUsernameUniqueAsync(string username, int? excludeUserId = null);

        // SOFT DELETE
        Task<bool> DeleteAsync(int id);
        Task<bool> RestoreAsync(int id);
        Task<PagedResultDto<UserDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10);
        Task<bool> PermanentDeleteAsync(int id);
    }
}