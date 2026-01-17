
namespace ISP.Application.DTOs.Plans
{
    public class UpdatePlanDto
    {
        public string? Name { get; set; }
        public int? Speed { get; set; }
        public decimal? Price { get; set; }
        public int? DurationDays { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}