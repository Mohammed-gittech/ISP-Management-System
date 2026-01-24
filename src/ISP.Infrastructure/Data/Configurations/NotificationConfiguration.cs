using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(n => n.Id);

            // Type: Enum → string
            builder.Property(n => n.Type)
                .HasConversion<string>()
                .IsRequired();

            // Message: مطلوب
            builder.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(500);

            // Channel: Enum → string
            builder.Property(n => n.Channel)
                .HasConversion<string>()
                .IsRequired();

            // Status: Enum → string
            builder.Property(n => n.Status)
                .HasConversion<string>()
                .IsRequired();

            // ErrorMessage: اختياري
            builder.Property(n => n.ErrorMessage)
                .HasMaxLength(500);

            // Indexes
            builder.HasIndex(n => n.TenantId);
            builder.HasIndex(n => n.SubscriberId);
            builder.HasIndex(n => n.Status);
            builder.HasIndex(n => n.SentDate);

            // Relationship: Notification → Tenant
            builder.HasOne(n => n.Tenant)
                .WithMany()
                .HasForeignKey(n => n.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Notification → Subscriber
            builder.HasOne(n => n.Subscriber)
                .WithMany(s => s.Notifications)
                .HasForeignKey(n => n.SubscriberId)
                .OnDelete(DeleteBehavior.Cascade); // حذف Subscriber = حذف إشعاراته
        }
    }
}