namespace TravelManagement.Core.DTOs
{
    public class SettleSettlementDTO
    {
        public int BookingId { get; set; }

        /// <summary>Omitted = settle the full remaining balance; otherwise a part payment.</summary>
        public decimal? PaidAmount { get; set; }

        /// <summary>Cash, UPI, Bank transfer, Other.</summary>
        public string? PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }

        /// <summary>When the money actually moved. Defaults to today.</summary>
        public DateOnly? PaidOn { get; set; }

        /// <summary>Client-generated once per payment form — a repeated submit is recorded once.</summary>
        public Guid? RequestId { get; set; }
    }

    public class ReverseSettlementPaymentDTO
    {
        public string? Reason { get; set; }
    }

    public class SettlementHistoryDTO
    {
        public int BookingId { get; set; }
        public string Direction { get; set; } = string.Empty;
        public decimal Due { get; set; }
        public decimal Settled { get; set; }
        public decimal Remaining { get; set; }
        /// <summary>Settled before itemised history existed (no individual rows for it).</summary>
        public decimal EarlierUnitemised { get; set; }
        public bool IsSettled { get; set; }
        public List<SettlementPaymentDTO> Payments { get; set; } = new();
    }

    public class SettlementPaymentDTO
    {
        public int SettlementPaymentId { get; set; }
        public int BookingId { get; set; }
        public string EntryType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public DateOnly PaidOn { get; set; }
        public int? ReversesPaymentId { get; set; }
        public bool IsReversed { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
