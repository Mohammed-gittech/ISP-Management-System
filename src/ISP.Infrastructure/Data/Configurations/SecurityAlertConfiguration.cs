using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class SecurityAlertConfiguration : IEntityTypeConfiguration<SecurityAlert>
    {
        public void Configure(EntityTypeBuilder<SecurityAlert> builder)
        {
            builder.ToTable("SecurityAlerts");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.AlertType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(e => e.IpAddress)
                .HasMaxLength(50);

            builder.Property(e => e.Username)
                .HasMaxLength(100);

            builder.Property(e => e.Severity)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Medium");

            builder.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("New");

            builder.Property(e => e.TelegramError)
                .HasMaxLength(500);

            builder.Property(e => e.ReviewNotes)
                .HasMaxLength(500);

            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}