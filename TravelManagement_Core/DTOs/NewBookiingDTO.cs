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
        // null = keep the existing status on edit (Pending for a new booking)
        public string? BookingStatus { get; set; }
        public string? ExternalEmployee { get; set; }
        public string? ExternalEmployeeNumber { get; set; }
        public double? commissionAmount { get; set; }
        public double Amount { get; set; }
        public string Payment { get; set; } = "Admin";
        public int? TravelAgentId { get; set; }
        public decimal? CustomerWillPay { get; set; }
        public decimal? OwnerWillPay { get; set; }
        public decimal? AdvancePay { get; set; }

        // Only populated when BookingType == SelfDriveRental
        public DateOnly? ReturnDate { get; set; }

        /// <summary>Sightseeing / full day ending with a drop elsewhere (airport, station, another hotel).</summary>
        public string? EndDrop { get; set; }

        /// <summary>Tour starting with a pickup (airport, station, another hotel).</summary>
        public string? StartPickup { get; set; }

        /// <summary>Full Day package: 8 (8 hr / 80 km) or 12 (12 hr / 120 km).</summary>
        public int? FullDayHours { get; set; }

        /// <summary>Self-drive rental: return time and refundable security deposit.</summary>
        public TimeOnly? ReturnTime { get; set; }
        public decimal? SecurityDeposit { get; set; }

        /// <summary>Add this booking (or package days) to an existing trip of the same customer.</summary>
        public int? TripId { get; set; }

        /// <summary>
        /// Hotel / stay for the trip. Sent once; every day of the trip reuses it. A package always
        /// gets a trip; a single booking gets one when a stay is given (so later bookings can join).
        /// </summary>
        public TripStayDTO? Stay { get; set; }
    }

    public class TripStayDTO
    {
        public string? Name { get; set; }
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public string? MapsLink { get; set; }
    }
}