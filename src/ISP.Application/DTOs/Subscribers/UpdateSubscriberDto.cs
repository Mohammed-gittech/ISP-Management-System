
using ISP.Domain.Enums;

namespace ISP.Application.DTOs.Subscribers
{
    public class UpdateSubscriberDto
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? TelegramUsername { get; set; }
        public string? NationalId { get; set; }
        public SubscriberStatus? Status { get; set; }
        public string? Notes { get; set; }
    }
}