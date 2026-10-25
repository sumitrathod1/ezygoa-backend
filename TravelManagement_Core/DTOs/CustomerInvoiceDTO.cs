namespace TravelManagement.Core.DTOs
{
    /// <summary>
    /// Customer invoice: one booking, the whole trip, or every booking of the customer.
    /// Organisation details come from the booking's own organisation (not hard-coded).
    /// </summary>
    public class CustomerInvoiceData
    {
        public string OrgName { get; set; } = string.Empty;
        public string? OrgPhone { get; set; }
        public string? OrgEmail { get; set; }
        public string? OrgAddress { get; set; }
        /// <summary>Organisation logo as a data URL (null = no logo)</summary>
        public string? OrgLogo { get; set; }

        public string InvoiceNo { get; set; } = string.Empty;
        public DateOnly InvoiceDate { get; set; }
        /// <summary>booking | trip | customer</summary>
        public string Scope { get; set; } = "booking";

        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
        public string? StayName { get; set; }

        public List<CustomerInvoiceLine> Lines { get; set; } = new();
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }

        /// <summary>How many bookings each scope would include (for the preview's choices).</summary>
        public int TripBookings { get; set; }
        public int CustomerBookings { get; set; }

        /// <summary>The invoice has a Full Day booking (terms are then included by default).</summary>
        public bool HasFullDay { get; set; }
        /// <summary>Terms &amp; conditions, when asked for — only those relevant to these bookings.</summary>
        public InvoiceTermsBlock? Terms { get; set; }
    }

    public class CustomerInvoiceLine
    {
        public int BookingId { get; set; }
        public DateOnly TravelDate { get; set; }
        public DateOnly? ReturnDate { get; set; }
        public TimeOnly? TravelTime { get; set; }
        public string BookingType { get; set; } = string.Empty;
        /// <summary>"North Goa Sightseeing + drop at Mopa Airport"</summary>
        public string TypeLabel { get; set; } = string.Empty;
        public string? EndDrop { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public int Pax { get; set; }
        /// <summary>Own vehicle / driver only — a partner's details are never put on a customer invoice.</summary>
        public string? Vehicle { get; set; }
        public string? Driver { get; set; }
        public decimal Amount { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
