
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