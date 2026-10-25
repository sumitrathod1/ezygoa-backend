using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// Terms &amp; conditions printed on customer invoices, one row per organisation and edited in
    /// Business Hub → Invoice terms (rates change; EzyGoa and ZipGoa may differ).
    /// The two tables are kept as JSON: they are always read and saved as a whole.
    /// </summary>
    public class InvoiceSettings
    {
        [Key]
        public int InvoiceSettingsId { get; set; }
        public int OrgId { get; set; }

        /// <summary>JSON: [{ label, from "19:00", to "00:00", amount, appliesTo "FullDay"|"Transfer"|"All" }]</summary>
        public string NightChargesJson { get; set; } = "[]";

        /// <summary>JSON: [{ category "Ertiga", perHour, perKm }]</summary>
        public string ExtraRatesJson { get; set; } = "[]";

        /// <summary>Any other terms (toll, parking, waiting…), one per line.</summary>
        public string? OtherTerms { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
