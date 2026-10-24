using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum SettlementEntryType
    {
        Payment  = 0,   // money moved in the direction that is due
        Reversal = 1,   // undoes an earlier Payment (financial rows are never deleted)
    }

    /// <summary>
    /// One money movement against a partner booking's settlement — the itemised
    /// history behind ExternalEmployeeCashCollection.TotalPaidToVendor.
    /// Rows are append-only: a mistake is corrected with a Reversal, not a delete.
    /// </summary>
    public class SettlementPayment
    {
        [Key]
        public int SettlementPaymentId { get; set; }

        public int SettlementId { get; set; }
        public ExternalEmployeeCashCollection? Settlement { get; set; }

        public int BookingId { get; set; }
        public int? ExternalEmployeeId { get; set; }

        public SettlementEntryType EntryType { get; set; }

        /// <summary>"VendorToOwner" (received from partner) or "OwnerToVendor" (paid to partner).</summary>
        public string Direction { get; set; } = string.Empty;

        /// <summary>Always positive. A Reversal subtracts this amount.</summary>
        public decimal Amount { get; set; }

        public string? PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }

        /// <summary>Date the money actually moved (may be earlier than CreatedAt).</summary>
        public DateOnly PaidOn { get; set; }

        /// <summary>Set on a Reversal: the Payment it undoes.</summary>
        public int? ReversesPaymentId { get; set; }

        /// <summary>Set on a Payment once it has been reversed.</summary>
        public bool IsReversed { get; set; }

        /// <summary>Client-generated key so a double-tapped Save records the payment once.</summary>
        public Guid? RequestId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }

        public int OrgId { get; set; } = 1;
    }
}
