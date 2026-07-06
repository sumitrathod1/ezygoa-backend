using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class RouteRate
    {
        [Key]
        public int RouteRateId { get; set; }

        public int FromPlaceId { get; set; }
        public Place? FromPlace { get; set; }

        public int ToPlaceId { get; set; }
        public Place? ToPlace { get; set; }

        [Required]
        public string VehicleType { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public decimal? PeakPrice { get; set; }

        public decimal? FloorPrice { get; set; }

        public bool IsActive { get; set; } = true;

        public int OrgId { get; set; } = 1;
    }
}
