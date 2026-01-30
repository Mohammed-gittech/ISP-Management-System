namespace ISP.Application.DTOs.AuditLogs
{
    public class AuditLogDto
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public string? TenantName { get; set; }

        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty; // Create, Update, Delete
        public string EntityType { get; set; } = string.Empty; // User, Subscriber
        public int? EntityId { get; set; }

        public string? OldValues { get; set; }
        public string? NewValues { get; set; }

        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }

        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}