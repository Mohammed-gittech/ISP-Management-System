
using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            builder.HasKey(u => u.Id);

            // Username: مطلوب، فريد
            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(u => u.Username)
                .IsUnique();

            // Email: مطلوب، فريد
            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(u => u.Email)
                .IsUnique();

            // PasswordHash: مطلوب
            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            // Role: Enum → string
            builder.Property(u => u.Role)
                .HasConversion<string>()
                .IsRequired();

            // Relationship: User → Tenant (اختياري للـ SuperAdmin)
            builder.HasOne(u => u.Tenant)
                .WithMany(u => u.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false); // TenantId can be null for SuperAdmin
        }
    }
}