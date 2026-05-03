namespace TravelManagement.Core.DTOs
{
    public class NewBookiingDTO
    {
        public int? BookingId { get; set; }
        public string? CustomerName { get; set; }
        public required string CustomerNumber { get; set; }
        public string? AlternateNumber { get; set; }
        public List<DayWiseBookingDTO>? DayWiseBookings { get; set; }
        public DateOnly BookingDate { get; set; }
        public TimeOnly BookingTime { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int Pax { get; set; }
        public int? VehicleId { get; set; }
        public int? UserId { get; set; }
        public string BookingType { get; set; } = "Notspecified";
        public string BookingStatus { get; set; } = "Pending";
        public string? ExternalEmployee { get; set; }
        public string? ExternalEmployeeNumber { get; set; }
        public double? commissionAmount { get; set; }
        public double Amount { get; set; }
        public string Payment { get; set; } = "Admin";
        public int? TravelAgentId { get; set; }
        public decimal? CustomerWillPay { get; set; }
        public decimal? OwnerWillPay { get; set; }
        public decimal? AdvancePay { get; set; }
    }
}