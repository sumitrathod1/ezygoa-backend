using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class RateChart
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string TemplateName { get; set; } = "Standard Rate Chart";
        public string? AgentName { get; set; }
        public string? AgentNumber { get; set; }
        public string CompanyName { get; set; } = "EZY GOA TRAVELS";
        public string? Tagline { get; set; }

        public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
        public DateTime ValidTo { get; set; } = DateTime.UtcNow.AddYears(1);
        public string? SpecialDaysNote { get; set; }
        public string? Locations { get; set; }

        public string VehiclesJson { get; set; } = "[]";
        public string RoutesJson { get; set; } = "[]";
        public string SurchargesJson { get; set; } = "[]";
        public string NotesJson { get; set; } = "[]";
        public string? FooterJson { get; set; }

        public string Currency { get; set; } = "INR";
        public string SeasonMode { get; set; } = "regular";
        public string? PeakSeasonDates { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Multi-tenancy
        public int OrgId { get; set; } = 1;
    }
}
