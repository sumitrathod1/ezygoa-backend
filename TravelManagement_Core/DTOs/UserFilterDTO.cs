using TravelManagement.Core.Models;

namespace TravelManagement.Core.DTOs
{
    public class UserFilterDTO
    {
        public int userId { get; set; }
        public DateOnly? PerticularDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public Status? Status { get; set; }
        public BookingType? BookingType { get; set; }
        public TimeOnly? TravelTime { get; set; }
    }
}