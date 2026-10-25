using TravelManagement.Core.Models;

namespace TravelManagement.Core.DTOs
{
    public class ExternalVendorSettlementDTO
    {
        public int BookingId { get; set; }
        public DateOnly BookingDate { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string VendorNumber { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public decimal BookingAmount { get; set; }
        public decimal Commission { get; set; }
        public CashCollectedBy CashCollectedBy { get; set; }
        public decimal VendorPayable { get; set; }
        public decimal TotalPaidToVendor { get; set; }
        public decimal PendingVendorPayment { get; set; }
        public decimal OwnerReceivable { get; set; }
        public string SettlementDirection { get; set; } = string.Empty;
        public bool IsSettled { get; set; }
        public DateTime? SettledAt { get; set; }

        public int? VendorId { get; set; }
        /// <summary>Customer advance already collected by Admin for this booking.</summary>
        public decimal AdvancePaid { get; set; }
        /// <summary>Received from the vendor so far (VendorToOwner direction).</summary>
        public decimal ReceivedFromVendor { get; set; }
        /// <summary>Still owed by the vendor (VendorToOwner direction).</summary>
        public decimal PendingReceivable { get; set; }
        /// <summary>What is still open on this booking, in whichever direction is due.</summary>
        public decimal Remaining { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
    }
}