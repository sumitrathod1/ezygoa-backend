using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TravelManagement.Core.Models
{
    public enum BookingType
    {
        FullDay,          // 0
        SightSeeing,      // 1
        Shuttle,          // 2
        AirportDrop,      // 3
        RailwayStation,   // 4
        Notspecified,     // 5
        AirportPickup,    // 6
        SelfDriveRental,  // 7 — do NOT insert above this; existing rows depend on 0-6
    }

    public enum Status
    {
        Completed,
        Canceled,
        Assigned,
        Pending
    }

    public enum Payment
    {
        Admin,
        ExternalEmployee
    }

    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public DateOnly travelDate { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int? VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }
        public int? Userid { get; set; }
        public User? user { get; set; }
        public BookingType BookingType { get; set; }
        public TimeOnly? Traveltime { get; set; }
        public Status Status { get; set; }
        public decimal Amount { get; set; }
        public int Pax { get; set; }
        public bool Assigned { get; set; }
        public int CustomerID { get; set; }
        public Customers? Customer { get; set; }
        public int? ExternalEmployeeId { get; set; }
        public ExternalEmployee? ExternalEmployee { get; set; }
        public Payment Payment { get; set; }
        public int? TravelAgentId { get; set; }
        public TravelAgent? TravelAgent { get; set; }
        public ICollection<Payment>? Payments { get; set; }
        public bool isValidAssignment => (Userid != null) ^ (ExternalEmployeeId != null);

        // Multi-tenancy
        public int OrgId { get; set; } = 1;

        // Audit: who created this booking
        public int? CreatedByUserId { get; set; }
        public User? CreatedBy      { get; set; }

        // Self-drive rentals: completion is triggered by ReturnDate (handled in AUTO-01 n8n workflow)
        public DateOnly? ReturnDate { get; set; }

        // Set when the booking was handed over from a linked organisation
        public int? TransferredFromOrgId { get; set; }
        public DateTime? TransferredAt { get; set; }
    }
}