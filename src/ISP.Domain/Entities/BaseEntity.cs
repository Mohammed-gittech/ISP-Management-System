// ============================================
// BaseEntity.cs
// ============================================
namespace ISP.Domain.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }

        //(Soft Delete Flag)
        public bool IsDeleted { get; set; } = false;

        /// DateTime = تاريخ الحذف (لحساب Retention Period)
        public DateTime? DeletedAt { get; set; }
    }
}