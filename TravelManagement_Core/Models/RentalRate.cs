using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class RentalRate
    {
        [Key]
        public int RentalRateId { get; set; }

        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public decimal PricePerDay { get; set; }
        public decimal? WeeklyRate { get; set; }
        public decimal SecurityDeposit { get; set; }
        public int MinDays { get; set; } = 1;
        public int? KmPerDay { get; set; }
        public decimal? ExtraKmRate { get; set; }

        public bool IsActive { get; set; } = true;
        public int OrgId { get; set; } = 1;
    }
}
