
namespace ISP.Application.DTOs.Plans
{
    public class CreatePlanDto
    {
        public string Name { get; set; } = string.Empty;
        public int Speed { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; } = 30;
        public string? Description { get; set; }
    }
}