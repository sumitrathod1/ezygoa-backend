namespace TravelManagement.Core.DTOs
{
    public class DayWiseBookingDTO
    {
        public DateOnly TravelDate { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public TimeOnly? TravelTime { get; set; } = null;
        public string? BookingType { get; set; }
        public decimal Amount { get; set; }
        public string? EndDrop { get; set; }
        public string? StartPickup { get; set; }
        public int? FullDayHours { get; set; }
    }
}