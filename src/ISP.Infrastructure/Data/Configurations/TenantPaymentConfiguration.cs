using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    /// <summary>
    /// TenantPayment FluentAPI Configuration
    /// </summary>
    public class TenantPaymentConfiguration : IEntityTypeConfiguration<TenantPayment>
    {
        public void Configure(EntityTypeBuilder<TenantPayment> builder)
        {
            builder.ToTable("TenantPayments");

            builder.HasKey(tp => tp.Id);

            // ============================================
            // Properties
            // ============================================

            builder.Property(tp => tp.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(tp => tp.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("USD");

            builder.Property(tp => tp.PaymentMethod)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(tp => tp.PaymentGateway)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(tp => tp.TransactionId)
                .HasMaxLength(255);

            builder.Property(tp => tp.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            builder.Property(tp => tp.InvoiceUrl)
                .HasMaxLength(500);

            builder.Property(tp => tp.ReceiptUrl)
                .HasMaxLength(500);

            builder.Property(tp => tp.Notes)
                .HasMaxLength(1000);

            builder.Property(tp => tp.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // ============================================
            // Foreign Keys
            // ============================================

            builder.HasOne(tp => tp.Tenant)
                .WithMany()
                .HasForeignKey(tp => tp.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(tp => tp.TenantSubscription)
                .WithMany()
                .HasForeignKey(tp => tp.TenantSubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================
            // Indexes
            // ============================================

            builder.HasIndex(tp => tp.TenantId);
            builder.HasIndex(tp => tp.TenantSubscriptionId);
            builder.HasIndex(tp => tp.Status);
            builder.HasIndex(tp => tp.CreatedAt);
            builder.HasIndex(tp => tp.TransactionId);

            // Composite index
            builder.HasIndex(tp => new { tp.TenantId, tp.Status, tp.CreatedAt });

            // ============================================
            // Query Filter (Soft Delete)
            // ============================================

            builder.HasQueryFilter(tp => !tp.IsDeleted);
        }
    }
}