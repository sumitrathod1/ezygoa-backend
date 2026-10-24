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
        /// <summary>
        /// Amount settled so far in the direction that is due — paid to the vendor when
        /// cash was collected by Admin, received from the vendor when the vendor collected.
        /// (Column name kept for backward compatibility.) Settlement rules live in
        /// BookingRepository so the list, settle action and booking edits agree.
        /// </summary>
        public decimal TotalPaidToVendor { get; set; } = 0;

        /// <summary>Fully settled once SettledAt is stamped (set when the amount due reaches 0).</summary>
        public bool IsSettled => SettledAt != null;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }
    }
}