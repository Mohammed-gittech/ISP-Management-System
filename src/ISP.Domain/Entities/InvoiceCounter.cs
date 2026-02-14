// ============================================
// InvoiceCounter.cs - عداد أرقام الفواتير
// النسخة الصحيحة: يرث من BaseEntity
// ============================================
namespace ISP.Domain.Entities
{
    /// <summary>
    /// عداد أرقام الفواتير لكل وكيل ولكل سنة
    /// يضمن تسلسل آمن من Database نفسها
    /// 
    /// ⭐ يرث من BaseEntity للحصول على Soft Delete (IsDeleted, DeletedAt)
    /// ⭐ CreatedAt و UpdatedAt منفصلة (ليست في BaseEntity)
    /// </summary>
    public class InvoiceCounter : BaseEntity
    {
        // Multi-Tenant Support
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // السنة (لإعادة التسلسل كل سنة)
        public int Year { get; set; }

        // آخر رقم مُستخدم
        public int LastNumber { get; set; }

        // ============================================
        // Timestamps (منفصلة - ليست في BaseEntity)
        // ============================================
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // موروثة من BaseEntity:
        // - int Id
        // - bool IsDeleted
        // - DateTime? DeletedAt
    }
}