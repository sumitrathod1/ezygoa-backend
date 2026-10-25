using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// One customer visit: groups the bookings of a multi-day package (airport pickup,
    /// sightseeing days, airport drop) and holds what they share — above all the hotel,
    /// entered once instead of on every booking. Money stays on the bookings.
    /// </summary>
    public class Trip
    {
        [Key]
        public int TripId { get; set; }

        public int OrgId { get; set; }

        public int CustomerId { get; set; }
        public Customers? Customer { get; set; }

        /// <summary>Hotel / stay name as picked on the map.</summary>
        public string? StayName { get; set; }
        public double? StayLat { get; set; }
        public double? StayLng { get; set; }
        public string? StayMapsLink { get; set; }

        public int Pax { get; set; }
        public string? Notes { get; set; }

        /// <summary>First and last travel date of the trip's bookings (kept in sync on save).</summary>
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
