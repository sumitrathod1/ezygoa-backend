namespace TravelManagement.Core.DTOs
{
    /// <summary>Everything a partner statement shows — and nothing internal (no expenses, no other partners).</summary>
    public class PartnerStatementData
    {
        public string OrgName { get; set; } = string.Empty;
        public string? OrgPhone { get; set; }
        public string? OrgEmail { get; set; }
        public string? OrgAddress { get; set; }
        /// <summary>Organisation logo as a data URL (null = no logo)</summary>
        public string? OrgLogo { get; set; }

        public int PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string PartnerNumber { get; set; } = string.Empty;

        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public int? BookingId { get; set; }
        public bool ShowFare { get; set; } = true;
        /// <summary>Only bookings that still have money to settle.</summary>
        public bool OnlyOpen { get; set; }
        /// <summary>Set when the statement covers one customer's bookings only.</summary>
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime GeneratedAt { get; set; }

        public List<PartnerStatementLine> Lines { get; set; } = new();
        public List<SettlementPaymentDTO> Payments { get; set; } = new();

        public decimal TotalFare { get; set; }
        /// <summary>Fare minus commission over the period — what the partner actually earns.</summary>
        public decimal TotalPartnerEarning { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal ReceivedFromPartner { get; set; }
        public decimal PaidToPartner { get; set; }
        public decimal PartnerStillOwes { get; set; }
        public decimal WeStillOwe { get; set; }
        /// <summary>Settled before itemised payment history existed.</summary>
        public decimal EarlierUnitemised { get; set; }
    }

    public class PartnerStatementLine
    {
        public int BookingId { get; set; }
        public DateOnly TravelDate { get; set; }
        public string Route { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal Fare { get; set; }
        public decimal Commission { get; set; }
        /// <summary>Fare minus commission.</summary>
        public decimal PartnerEarning { get; set; }
        public string CashCollectedBy { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal Due { get; set; }
        public decimal Settled { get; set; }
        public decimal Remaining { get; set; }
        public bool Cancelled { get; set; }
    }
}
