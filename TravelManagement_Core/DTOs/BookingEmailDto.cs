namespace TravelManagement.Core.DTOs
{
    public class BookingEmailDto
    {
        public string? CustomerName { get; set; }
        public string? CustomerNumber { get; set; }
        public string? TravelDate { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? Pax { get; set; }
        public string? VehicleName { get; set; }
    }
}