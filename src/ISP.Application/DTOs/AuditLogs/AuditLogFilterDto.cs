// ============================================
// AuditLogFilterDto.cs - فلترة السجلات
// ============================================
namespace ISP.Application.DTOs.AuditLogs
{
    public class AuditLogFilterDto
    {
        public int? TenantId { get; set; }
        public int? UserId { get; set; }
        public string? Action { get; set; } // Create, Update, Delete, Login
        public string? EntityType { get; set; } // User, Subscriber, Plan, Subscription
        public int? EntityId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? Success { get; set; } // true = نجحت، false = فشلت، null = الكل
        public string? SearchTerm { get; set; } // بحث في Username أو IpAddress

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}