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
        /// <summary>Who added the booking</summary>
        public int? CreatedByUserId { get; set; }
        /// <summary>Handed over by this linked organisation</summary>
        public int? FromOrgId { get; set; }
        /// <summary>true = only bookings handed over by any linked organisation</summary>
        public bool? TransferredOnly { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}