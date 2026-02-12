using ISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ISP.Infrastructure.Data.Configurations
{
    /// <summary>
    /// Payment FluentAPI Configuration
    /// </summary>
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");

            builder.HasKey(p => p.Id);

            // ============================================
            // Properties
            // ============================================

            builder.Property(p => p.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("IQD");

            builder.Property(p => p.PaymentMethod)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.PaymentGateway)
                .HasMaxLength(50);

            builder.Property(p => p.TransactionId)
                .HasMaxLength(255);

            builder.Property(p => p.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            builder.Property(p => p.CashReceiptNumber)
                .HasMaxLength(100);

            builder.Property(p => p.Notes)
                .HasMaxLength(1000);

            builder.Property(p => p.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // ============================================
            // Foreign Keys
            // ============================================

            builder.HasOne(p => p.Tenant)
                .WithMany()
                .HasForeignKey(p => p.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Subscriber)
                .WithMany()
                .HasForeignKey(p => p.SubscriberId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Subscription)
                .WithMany()
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Invoice)
                .WithOne(i => i.Payment)
                .HasForeignKey<Payment>(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.ReceivedByUser)
                .WithMany()
                .HasForeignKey(p => p.ReceivedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================
            // Indexes
            // ============================================

            builder.HasIndex(p => p.TenantId);
            builder.HasIndex(p => p.SubscriberId);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => p.PaymentMethod);
            builder.HasIndex(p => p.CreatedAt);
            builder.HasIndex(p => p.TransactionId);

            // Composite index للبحث السريع
            builder.HasIndex(p => new { p.TenantId, p.Status, p.CreatedAt });

            // ============================================
            // Query Filter (Soft Delete)
            // ============================================

            builder.HasQueryFilter(p => !p.IsDeleted);
        }
    }
}