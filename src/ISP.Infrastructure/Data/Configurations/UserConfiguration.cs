
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

            // UNIQUE INDEXES (Filtered for Active Users Only)
            // Username فريد (فقط للمستخدمين النشطين)
            builder.HasIndex(u => u.Username)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0") // ✅ Filtered Index
                .HasDatabaseName("IX_Users_Username_Unique");

            // Email: مطلوب، فريد
            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(100);

            // Email فريد (فقط للمستخدمين النشطين)
            builder.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0") // ✅ Filtered Index
                .HasDatabaseName("IX_Users_Email_Unique");

            // PasswordHash: مطلوب
            builder.Property(u => u.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            // Role: Enum → string
            builder.Property(u => u.Role)
                .HasConversion<string>()
                .IsRequired();

            // SOFT DELETE SUPPORT     
            // IsDeleted: Index لتسريع الاستعلامات       
            builder.HasIndex(u => new { u.TenantId, u.IsDeleted })
                .HasDatabaseName("IX_Users_TenantId_IsDeleted");

            // DeletedAt: Index لـ Retention Cleanup
            builder.HasIndex(u => new { u.IsDeleted, u.DeletedAt })
                .HasDatabaseName("IX_Users_IsDeleted_DeletedAt");

            // Relationship: User → Tenant (اختياري للـ SuperAdmin)
            builder.HasOne(u => u.Tenant)
                .WithMany(u => u.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false); // TenantId can be null for SuperAdmin

            // Account Lockout 
            builder.Property(u => u.FailedLoginAttempts)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(u => u.LockoutEnd)
                .IsRequired(false);

            builder.Property(u => u.LastFailedLoginAt)
                .IsRequired(false);

            builder.Ignore(u => u.IsLockedOut);
            builder.Ignore(u => u.LockoutRemainingMinutes);

            builder.HasIndex(u => u.LockoutEnd)
            .HasDatabaseName("IX_Users_LockoutEnd");

        }
    }
}