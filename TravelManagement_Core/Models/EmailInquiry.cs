using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum LeadStatus
    {
        New          = 0,
        Quoted       = 1,
        Negotiating  = 2,
        Won          = 3,
        Lost         = 4,
        Cold         = 5
    }

    public class EmailInquiry
    {
        [Key]
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public DateOnly? TravelDate { get; set; }
        public int Pax { get; set; }
        public string? VehicleName { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public bool IsRejected { get; set; } = false;
        public DateOnly? CreatedAt { get; set; }

        // Lead funnel columns
        public string? Source { get; set; }
        public LeadStatus? LeadStatus { get; set; }
        public decimal? QuotedAmount { get; set; }
        public DateTime? NextFollowUpAt { get; set; }
        public string? LostReason { get; set; }
        public int? ConvertedBookingId { get; set; }
        public Booking? ConvertedBooking { get; set; }
    }
}