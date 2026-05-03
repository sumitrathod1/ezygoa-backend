using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum CashCollectedBy
    {
        Admin,
        ExternalEmployee
    }

    public class ExternalEmployeeCashCollection
    {
        [Key]
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
        public int? ExternalEmployeeId { get; set; }
        public ExternalEmployee? ExternalEmployee { get; set; }
        public CashCollectedBy CashCollectedBy { get; set; }
        public decimal BookingAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal PayableToVendor => BookingAmount - CommissionAmount;
        public decimal TotalPaidToVendor { get; set; } = 0;
        public bool IsSettled => TotalPaidToVendor >= PayableToVendor;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }
    }
}