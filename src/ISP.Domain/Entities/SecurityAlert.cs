namespace ISP.Domain.Entities
{
    /// <summary>
    /// Represents a security alert triggered by suspicious activity
    /// </summary>
    public class SecurityAlert : BaseEntity
    {
        // Type of alert — what triggered it
        public string AlertType { get; set; } = string.Empty;

        // Human-readable description of the alert
        public string Message { get; set; } = string.Empty;

        // IP address that triggered the alert (masked)
        public string? IpAddress { get; set; }

        // Username or email involved (masked)
        public string? Username { get; set; }

        // Number of occurrences that triggered the alert
        public int OccurrenceCount { get; set; }

        // Severity level: Low, Medium, High, Critical
        public string Severity { get; set; } = "Medium";

        // Alert status: New, Reviewed, Resolved, Ignored
        public string Status { get; set; } = "New";

        // Was Telegram notification sent successfully?
        public bool TelegramSent { get; set; } = false;

        // Error message if Telegram failed
        public string? TelegramError { get; set; }

        // When the alert was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When SuperAdmin reviewed it
        public DateTime? ReviewedAt { get; set; }

        // Notes added by SuperAdmin during review
        public string? ReviewNotes { get; set; }
    }
}