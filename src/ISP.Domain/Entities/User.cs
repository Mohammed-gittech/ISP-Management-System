// ============================================
// User.cs - المستخدمين (Admins, Employees)
// ============================================

using ISP.Domain.Enums;

namespace ISP.Domain.Entities
{
    public class User : BaseEntity
    {
        public int? TenantId { get; set; } // null = SuperAdmin
        public Tenant? Tenant { get; set; }

        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Employee;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Account Lockout
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEnd { get; set; } = null;
        public DateTime? LastFailedLoginAt { get; set; } = null;

        // Computed Properties
        public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTime.UtcNow;
        public int LockoutRemainingMinutes =>
        IsLockedOut ? (int)Math.Ceiling((LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes) : 0;

    }
}