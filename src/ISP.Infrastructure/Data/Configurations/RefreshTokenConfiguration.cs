using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            // اسم الجدول
            builder.ToTable("RefreshTokens");

            // Primary Key
            builder.HasKey(r => r.Id);

            // Token

            builder.Property(r => r.Token)
                .IsRequired()
                .HasMaxLength(500);

            builder.HasIndex(r => r.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token_Unique");

            // UserId — الارتباط بالمستخدم
            builder.Property(r => r.UserId)
                .IsRequired();

            // Dates
            builder.Property(r => r.CreatedAt)
                .IsRequired();

            builder.Property(r => r.ExpiresAt)
                .IsRequired();

            builder.Property(r => r.RevokedAt)
                .IsRequired(false);

            // Computed Properties
            builder.Ignore(r => r.IsExpired);
            builder.Ignore(r => r.IsActive);

            // User العلاقة مع
            builder.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Index 
            builder.HasIndex(r => new { r.UserId, r.IsRevoked })
                .HasDatabaseName("IX_RefreshTokens_UserId_IsRevoked");

            builder.HasQueryFilter(r => !r.IsRevoked);
        }
    }
}