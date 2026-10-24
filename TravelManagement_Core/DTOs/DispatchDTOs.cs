namespace TravelManagement.Core.DTOs
{
    /// <summary>
    /// Change the driver and/or vehicle of a booking. Fields left null are kept;
    /// ClearVehicle / ClearDriver remove the current one.
    /// </summary>
    public class AssignBookingDTO
    {
        public int? VehicleId { get; set; }
        public int? UserId { get; set; }
        public bool ClearVehicle { get; set; }
        public bool ClearDriver { get; set; }
    }

    public class AssignBookingResult
    {
        public int BookingId { get; set; }
        public int? VehicleId { get; set; }
        public string? VehicleName { get; set; }
        public int? UserId { get; set; }
        public string? DriverName { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool FullyAssigned { get; set; }
        /// <summary>Same driver / vehicle already booked that day — shown, not blocked.</summary>
        public List<string> Warnings { get; set; } = new();
        public int OrgId { get; set; }
    }

    public class TransferBookingDTO
    {
        public int TargetOrgId { get; set; }
    }

    public class TransferBookingResult
    {
        public int BookingId { get; set; }
        public int FromOrgId { get; set; }
        public string FromOrgName { get; set; } = string.Empty;
        public int ToOrgId { get; set; }
        public string ToOrgName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class OrgLinkDTO
    {
        public int OrgAId { get; set; }
        public int OrgBId { get; set; }
    }
}
