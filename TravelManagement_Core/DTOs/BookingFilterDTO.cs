using TravelManagement.Core.Models;

namespace TravelManagement.Core.DTOs
{
    public class BookingFilterDTO
    {
        public DateOnly? PerticularDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public Status? Status { get; set; }
        public int? VehicleId { get; set; }
        public int? UserId { get; set; }
        public BookingType? BookingType { get; set; }
        public TimeOnly? TravelTime { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}