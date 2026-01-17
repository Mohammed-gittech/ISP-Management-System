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
    }
}