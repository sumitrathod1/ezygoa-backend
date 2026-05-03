using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // JSON columns — stored as nvarchar(max), serialized by service layer
        [Column(TypeName = "nvarchar(max)")]
        public string VehiclesJson { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string RoutesJson { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string SurchargesJson { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string NotesJson { get; set; } = "[]";

        [Column(TypeName = "nvarchar(max)")]
        public string? FooterJson { get; set; }

        public string Currency { get; set; } = "INR";
        public string SeasonMode { get; set; } = "regular";
        public string? PeakSeasonDates { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
