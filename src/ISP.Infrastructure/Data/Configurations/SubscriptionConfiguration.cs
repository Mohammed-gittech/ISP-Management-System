
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions");

            builder.HasKey(sub => sub.Id);

            // StartDate: مطلوب
            builder.Property(sub => sub.StartDate)
                .IsRequired();

            // EndDate: مطلوب
            builder.Property(sub => sub.EndDate)
                .IsRequired();

            // Status: Enum → string
            builder.Property(sub => sub.Status)
                .HasConversion<string>()
                .IsRequired();

            // AutoRenew: Default false
            builder.Property(sub => sub.AutoRenew)
                .IsRequired()
                .HasDefaultValue(false);

            // Notes: اختياري
            builder.Property(sub => sub.Notes)
                .HasMaxLength(500);

            // Indexes
            builder.HasIndex(sub => sub.TenantId);
            builder.HasIndex(sub => sub.SubscriberId);
            builder.HasIndex(sub => sub.Status);
            builder.HasIndex(sub => sub.EndDate); // للبحث عن الاشتراكات المنتهية

            // Relationship: Subscription → Tenant
            builder.HasOne(sub => sub.Tenant)
                .WithMany(t => t.Subscriptions)
                .HasForeignKey(sub => sub.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship: Subscription → Subscriber
            builder.HasOne(sub => sub.Subscriber)
                .WithMany(s => s.Subscriptions)
                .HasForeignKey(sub => sub.SubscriberId)
                .OnDelete(DeleteBehavior.Restrict); // لا يمكن حذف Subscriber له Subscriptions

            // Relationship: Subscription → Plan
            builder.HasOne(sub => sub.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(sub => sub.PlanId)
                .OnDelete(DeleteBehavior.Restrict); // لا يمكن حذف Plan مستخدم
        }
    }
}