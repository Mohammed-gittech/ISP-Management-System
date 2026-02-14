using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    /// <summary>
    /// تكوين جدول InvoiceCounter
    /// النسخة الصحيحة: InvoiceCounter يرث من BaseEntity
    /// Query Filter تلقائي من ApplicationDbContext
    /// </summary>
    public class InvoiceCounterConfiguration : IEntityTypeConfiguration<InvoiceCounter>
    {
        public void Configure(EntityTypeBuilder<InvoiceCounter> builder)
        {
            builder.ToTable("InvoiceCounters");

            builder.HasKey(c => c.Id);

            // TenantId: مطلوب
            builder.Property(c => c.TenantId)
                .IsRequired();

            // Year: مطلوب
            builder.Property(c => c.Year)
                .IsRequired();

            // LastNumber: مطلوب، افتراضياً 0
            builder.Property(c => c.LastNumber)
                .IsRequired()
                .HasDefaultValue(0);

            // ============================================
            // Timestamps (منفصلة - ليست في BaseEntity)
            // ============================================
            builder.Property(c => c.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(c => c.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // ============================================
            // UNIQUE INDEX على (TenantId, Year) ⭐ مهم جداً!
            // ============================================
            builder.HasIndex(c => new { c.TenantId, c.Year })
                .IsUnique()
                .HasDatabaseName("IX_InvoiceCounters_TenantId_Year")
                .HasFilter("[IsDeleted] = 0"); // ⭐ فقط السجلات غير المحذوفة

            // Index على TenantId للأداء
            builder.HasIndex(c => c.TenantId)
                .HasDatabaseName("IX_InvoiceCounters_TenantId");

            // Relationship مع Tenant
            builder.HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}