using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Payments
    {
        [Key]
        public int PaymentId { get; set; }
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? PaymentMethod { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
        public int? TravelAgentId { get; set; }
        public TravelAgent? TravelAgent { get; set; }
        public int? CustomerId { get; set; }
        public Customers? Customer { get; set; }
    }
}