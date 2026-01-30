// ============================================
// AuditLogConfiguration.cs - إعدادات جدول AuditLogs
// ============================================
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");

            builder.HasKey(a => a.Id);

            // Username
            builder.Property(a => a.Username)
                .IsRequired()
                .HasMaxLength(100);

            // Action
            builder.Property(a => a.Action)
                .IsRequired()
                .HasMaxLength(50);

            // EntityType
            builder.Property(a => a.EntityType)
                .IsRequired()
                .HasMaxLength(100);

            // OldValues & NewValues (JSON)
            builder.Property(a => a.OldValues)
                .HasColumnType("nvarchar(max)");

            builder.Property(a => a.NewValues)
                .HasColumnType("nvarchar(max)");

            // IpAddress
            builder.Property(a => a.IpAddress)
                .IsRequired()
                .HasMaxLength(50);

            // UserAgent
            builder.Property(a => a.UserAgent)
                .HasMaxLength(500);

            // ErrorMessage
            builder.Property(a => a.ErrorMessage)
                .HasMaxLength(2000);

            // Timestamp Index (للبحث السريع)
            builder.HasIndex(a => a.Timestamp);

            // Action Index
            builder.HasIndex(a => a.Action);

            // EntityType + EntityId Index
            builder.HasIndex(a => new { a.EntityType, a.EntityId });

            // Foreign Keys
            builder.HasOne(a => a.Tenant)
                .WithMany()
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull); // إذا حُذف المستخدم، UserId = null
        }
    }
}