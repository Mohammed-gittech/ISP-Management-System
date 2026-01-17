// ============================================
// UserRole.cs - دور المستخدم
// ============================================
namespace ISP.Domain.Enums
{
    public enum UserRole
    {
        SuperAdmin = 0,    // مدير النظام (أنت)
        TenantAdmin = 1,   // مدير الوكيل
        Employee = 2       // موظف
    }
}