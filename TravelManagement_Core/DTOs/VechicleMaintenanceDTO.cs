namespace TravelManagement.Core.DTOs
{
    public class VechicleMaintenanceDTO
    {
        public DateTime ServieDate { get; set; }
        public DateTime Nextduedate { get; set; }
        public string? Description { get; set; }
        public decimal cost { get; set; }
        public string maintenanceType { get; set; } = "Service";
        public int VehicleID { get; set; }
    }
}