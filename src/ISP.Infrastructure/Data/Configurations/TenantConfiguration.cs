
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("Tenants");

            builder.HasKey(t => t.Id);

            // Name: مطلوب
            builder.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(100);

            // Subdomain: اختياري، فريد
            builder.Property(t => t.Subdomain)
                .HasMaxLength(50);

            builder.HasIndex(t => t.Subdomain)
                .IsUnique()
                .HasFilter("[Subdomain] IS NOT NULL"); // فقط للقيم غير الـ null

            // ContactEmail: مطلوب
            builder.Property(t => t.ContactEmail)
                .IsRequired()
                .HasMaxLength(100);

            // ContactPhone: اختياري
            builder.Property(t => t.ContactPhone)
                .HasMaxLength(20);

            // SubscriptionPlan: Enum يُحفظ كـ string
            builder.Property(t => t.SubscriptionPlan)
                .HasConversion<string>()
                .IsRequired();

            // TelegramBotToken: اختياري
            builder.Property(t => t.TelegramBotToken)
                .HasMaxLength(200);
        }
    }
}