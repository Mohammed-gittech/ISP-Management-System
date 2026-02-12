// ============================================
// InvoiceDto.cs - Invoice DTOs
// ============================================
namespace ISP.Application.DTOs.Invoices
{
    /// <summary>
    /// DTO للفاتورة
    /// </summary>
    public class InvoiceDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int SubscriberId { get; set; }
        public string SubscriberName { get; set; } = string.Empty;
        public string SubscriberPhone { get; set; } = string.Empty;
        public string? SubscriberAddress { get; set; }

        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>
        /// عناصر الفاتورة (parsed من JSON)
        /// </summary>
        public List<InvoiceItemDto> Items { get; set; } = new();

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public string Currency { get; set; } = "IQD";

        public string Status { get; set; } = string.Empty;

        public DateTime? DueDate { get; set; }
        public DateTime IssuedDate { get; set; }
        public DateTime? PaidDate { get; set; }

        public int? PaymentId { get; set; }
        public string? PaymentMethod { get; set; }

        public DateTime? PrintedAt { get; set; }
        public int PrintCount { get; set; }

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO لعنصر في الفاتورة
    /// </summary>
    public class InvoiceItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Total => Quantity * UnitPrice;
        public string? Description { get; set; }
    }

    /// <summary>
    /// DTO لقائمة الفواتير (مبسط)
    /// </summary>
    public class InvoiceListDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SubscriberName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Currency { get; set; } = "IQD";
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedDate { get; set; }
        public DateTime? PaidDate { get; set; }
    }

    /// <summary>
    /// معلومات الفاتورة للطباعة (كاملة مع معلومات الشركة)
    /// </summary>
    public class InvoicePrintDto
    {
        // معلومات الشركة (Tenant)
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyAddress { get; set; }
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string? CompanyLogo { get; set; } // Base64 or URL

        // معلومات الفاتورة
        public InvoiceDto Invoice { get; set; } = new();

        // معلومات إضافية للطباعة
        public string QRCodeData { get; set; } = string.Empty; // للـ QR Code
        public string BarcodeData { get; set; } = string.Empty; // للـ Barcode
        public string PrintedBy { get; set; } = string.Empty; // اسم الموظف
        public DateTime PrintedAt { get; set; }
    }
}