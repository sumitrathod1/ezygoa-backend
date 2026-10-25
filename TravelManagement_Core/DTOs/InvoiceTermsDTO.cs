namespace TravelManagement.Core.DTOs
{
    public class InvoiceTermsDTO
    {
        public List<NightChargeDTO> NightCharges { get; set; } = new();
        public List<ExtraRateDTO> ExtraRates { get; set; } = new();
        public string? OtherTerms { get; set; }
    }

    /// <summary>A night charge window, e.g. 19:00–00:00 ₹500 for Full Day bookings.</summary>
    public class NightChargeDTO
    {
        public string Label { get; set; } = string.Empty;
        /// <summary>"HH:mm"</summary>
        public string From { get; set; } = "19:00";
        /// <summary>"HH:mm" — may be past midnight (from 19:00 to 08:00)</summary>
        public string To { get; set; } = "00:00";
        public decimal Amount { get; set; }
        /// <summary>FullDay | Transfer (airport / station pickup &amp; drop) | All</summary>
        public string AppliesTo { get; set; } = "FullDay";
    }

    /// <summary>Extra hour / km after the Full Day package, per vehicle category.</summary>
    public class ExtraRateDTO
    {
        public string Category { get; set; } = string.Empty;
        public decimal PerHour { get; set; }
        public decimal PerKm { get; set; }
    }

    /// <summary>The terms that apply to one invoice (only what is relevant to its bookings).</summary>
    public class InvoiceTermsBlock
    {
        public List<NightChargeDTO> NightCharges { get; set; } = new();
        /// <summary>"after 8 hr / 80 km" etc. for the Full Day bookings on the invoice.</summary>
        public List<string> Packages { get; set; } = new();
        public List<ExtraRateDTO> ExtraRates { get; set; } = new();
        /// <summary>True when only the booked vehicle's row is shown.</summary>
        public bool ExtraRatesForVehicle { get; set; }
        public List<string> OtherTerms { get; set; } = new();
    }
}
