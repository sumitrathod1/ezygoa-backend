using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum PayerType
    {
        Customer,
        Owner,
        Agent
    }

    public class BookingPaymentAllocation
    {
        [Key]
        public int PaymentAllocationId { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
        public PayerType PayerType { get; set; }
        public int? CustomerId { get; set; }
        public Customers? Customers { get; set; }
        public int? TravelAgentId { get; set; }
        public TravelAgent? TravelAgent { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal PaidAmount { get; set; } = 0;
    }
}