// ============================================
// AuditLog.cs - سجل العمليات
// ============================================
namespace ISP.Domain.Entities
{
    public class AuditLog : BaseEntity
    {
        // Multi-Tenant Support
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // من عمل العملية
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string Username { get; set; } = string.Empty; // نسخة احتياطية إذا حُذف المستخدم

        // تفاصيل العملية
        public string Action { get; set; } = string.Empty; // Create, Update, Delete, Login, Logout
        public string EntityType { get; set; } = string.Empty; // User, Subscriber, Plan, Subscription
        public int? EntityId { get; set; } // معرف الكيان المتأثر

        // البيانات (JSON)
        public string? OldValues { get; set; } // القيم قبل التعديل
        public string? NewValues { get; set; } // القيم بعد التعديل

        // معلومات تقنية
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; } // Browser/Device info

        // النتيجة
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }
}