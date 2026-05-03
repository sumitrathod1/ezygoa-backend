using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum MaintenanceType
    {
        oilChange,
        TireChange,
        Service
    }

    public class VehicleMaintenanceShedule
    {
        [Key]
        public int VechicleMaintenanceSheduleId { get; set; }
        public DateTime ServieDate { get; set; }
        public DateTime Nextduedate { get; set; }
        public string? Description { get; set; }
        public decimal cost { get; set; }
        public MaintenanceType maintenanceType { get; set; }
        public int VehicleID { get; set; }
        public Vehicle? Vehicle { get; set; }
    }
}