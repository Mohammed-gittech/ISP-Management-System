
namespace ISP.Application.DTOs.Plans
{
    public class PlanDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Speed { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int ActiveSubscriptionsCount { get; set; }
    }
}