using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Invoice FluentAPI Configuration
    /// </summary>
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.ToTable("Invoices");

            builder.HasKey(i => i.Id);

            // ============================================
            // Properties
            // ============================================

            builder.Property(i => i.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(i => i.Items)
                .HasColumnType("nvarchar(max)"); // JSON

            builder.Property(i => i.Subtotal)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.Tax)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(i => i.Discount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(i => i.Total)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(i => i.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("IQD");

            builder.Property(i => i.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Unpaid");

            builder.Property(i => i.Notes)
                .HasMaxLength(1000);

            builder.Property(i => i.IssuedDate)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            builder.Property(i => i.PrintCount)
                .HasDefaultValue(0);

            builder.Property(i => i.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // ============================================
            // Foreign Keys
            // ============================================

            builder.HasOne(i => i.Tenant)
                .WithMany()
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(i => i.Subscriber)
                .WithMany()
                .HasForeignKey(i => i.SubscriberId)
                .OnDelete(DeleteBehavior.Restrict);

            // Payment relationship (configured in PaymentConfiguration)

            // ============================================
            // Indexes
            // ============================================

            builder.HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            builder.HasIndex(i => i.TenantId);
            builder.HasIndex(i => i.SubscriberId);
            builder.HasIndex(i => i.Status);
            builder.HasIndex(i => i.IssuedDate);

            // Composite index
            builder.HasIndex(i => new { i.TenantId, i.Status, i.IssuedDate });

            // ============================================
            // Query Filter (Soft Delete)
            // ============================================

            builder.HasQueryFilter(i => !i.IsDeleted);
        }
    }
}