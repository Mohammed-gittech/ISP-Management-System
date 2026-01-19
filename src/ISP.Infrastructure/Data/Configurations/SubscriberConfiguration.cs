
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class SubscriberConfiguration : IEntityTypeConfiguration<Subscriber>
    {
        public void Configure(EntityTypeBuilder<Subscriber> builder)
        {
            builder.ToTable("Subscribers");

            builder.HasKey(s => s.Id);

            // FullName: مطلوب
            builder.Property(s => s.FullName)
                .IsRequired()
                .HasMaxLength(100);

            // PhoneNumber: مطلوب
            builder.Property(s => s.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            // Email: اختياري
            builder.Property(s => s.Email)
                .HasMaxLength(100);

            // Address: اختياري
            builder.Property(s => s.Address)
                .HasMaxLength(200);

            // TelegramChatId: اختياري
            builder.Property(s => s.TelegramChatId)
                .HasMaxLength(100);

            // TelegramUsername: اختياري
            builder.Property(s => s.TelegramUsername)
                .HasMaxLength(50);

            // NationalId: اختياري
            builder.Property(s => s.NationalId)
                .HasMaxLength(20);

            // Status: Enum → string
            builder.Property(s => s.Status)
                .HasConversion<string>()
                .IsRequired();

            // Notes: اختياري
            builder.Property(s => s.Notes)
                .HasMaxLength(500);

            // Indexes
            builder.HasIndex(s => s.TenantId);

            // PhoneNumber فريد لكل Tenant (Composite Index)
            builder.HasIndex(s => new { s.TenantId, s.PhoneNumber })
                .IsUnique();

            // Relationship: Subscriber → Tenant
            builder.HasOne(s => s.Tenant)
                .WithMany(t => t.Subscribers)
                .HasForeignKey(s => s.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // لا يمكن حذف Tenant له Subscribers
        }
    }
}