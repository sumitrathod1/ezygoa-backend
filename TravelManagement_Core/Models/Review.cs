using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        public string? Comment { get; set; }
        public string? Source { get; set; }
        public DateOnly? TravelDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int OrgId { get; set; } = 1;
    }
}
