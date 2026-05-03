namespace TravelManagement.Core.DTOs
{
    public class UpdateVehicleDTO
    {
        public int VehicleId { get; set; }
        public string? VehicleName { get; set; }
        public string? VehicleNumber { get; set; }
        public string VehicleType { get; set; } = string.Empty;
        public DateOnly RegistrationDate { get; set; }
        public int Seatingcapacity { get; set; }
    }
}