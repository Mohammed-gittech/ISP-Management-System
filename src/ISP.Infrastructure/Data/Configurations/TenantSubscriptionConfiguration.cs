
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
    {
        public void Configure(EntityTypeBuilder<TenantSubscription> builder)
        {
            builder.ToTable("TenantSubscriptions");

            builder.HasKey(ts => ts.Id);

            // Plan: Enum → string
            builder.Property(ts => ts.Plan)
                .HasConversion<string>()
                .IsRequired();

            // Price: Decimal(18,2)
            builder.Property(ts => ts.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            // Status: Enum → string
            builder.Property(ts => ts.Status)
                .HasConversion<string>()
                .IsRequired();

            // PaymentMethod: اختياري
            builder.Property(ts => ts.PaymentMethod)
                .HasMaxLength(50);

            // Relationship: TenantSubscription → Tenant
            builder.HasOne(ts => ts.Tenant)
                .WithMany(ts => ts.TenantSubscriptions)
                .HasForeignKey(ts => ts.TenantId)
                .OnDelete(DeleteBehavior.Cascade); // حذف Tenant = حذف اشتراكاته
        }
    }
}