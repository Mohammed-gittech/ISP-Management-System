namespace ISP.Application.DTOs.SecurityAlerts
{
    /// <summary>
    /// Data transfer object for SecurityAlert responses
    /// </summary>
    public class SecurityAlertDto
    {
        public int Id { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? Username { get; set; }
        public int OccurrenceCount { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool TelegramSent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}