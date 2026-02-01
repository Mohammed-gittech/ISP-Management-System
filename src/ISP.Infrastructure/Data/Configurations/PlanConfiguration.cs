
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("Plans");

            builder.HasKey(p => p.Id);

            // Name: مطلوب
            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(50);

            // Speed: مطلوب
            builder.Property(p => p.Speed)
                .IsRequired();

            // Price: Decimal(18,2)
            builder.Property(p => p.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            // DurationDays: مطلوب
            builder.Property(p => p.DurationDays)
                .IsRequired();

            // Description: اختياري
            builder.Property(p => p.Description)
                .HasMaxLength(200);

            // SOFT DELETE SUPPORT
            // IsDeleted: Index لتسريع الاستعلامات
            builder.HasIndex(p => new { p.TenantId, p.IsDeleted })
                .HasDatabaseName("IX_Plans_TenantId_IsDeleted");

            // DeletedAt: Index لـ Retention Cleanup
            builder.HasIndex(p => new { p.IsDeleted, p.DeletedAt })
                .HasDatabaseName("IX_Plans_IsDeleted_DeletedAt");

            // Index
            builder.HasIndex(p => p.TenantId);

            // Relationship: Plan → Tenant
            builder.HasOne(p => p.Tenant)
                .WithMany(t => t.Plans)
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Cascade); // حذف Tenant = حذف باقاته
        }
    }
}