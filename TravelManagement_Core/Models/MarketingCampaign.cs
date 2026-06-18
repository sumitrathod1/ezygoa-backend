using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class MarketingCampaign
    {
        [Key]
        public int CampaignId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Channel { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public decimal? Budget { get; set; }
        public decimal Spent { get; set; } = 0;
        public int LeadsGenerated { get; set; } = 0;
        public int BookingsConverted { get; set; } = 0;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int OrgId { get; set; } = 1;
    }
}
