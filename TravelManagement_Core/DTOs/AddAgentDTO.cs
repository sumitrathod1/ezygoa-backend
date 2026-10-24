using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.DTOs
{
    public class AddAgentDTO
    {
        public string Name { get; set; } = string.Empty;

        [Phone]
        public string ContactNumber { get; set; } = string.Empty;
        public string AgentType { get; set; } = string.Empty;

        // Optional: hotels and vehicle owners often have no email, and the form sends ""
        // for a blank field — [EmailAddress] rejected that, so such agents could not be saved.
        [OptionalEmail]
        public string? Email { get; set; }

        // Extended fields
        public string? ContactPerson { get; set; }
        public string? WhatsApp { get; set; }
        public string? Address { get; set; }
        public decimal CommissionPercent { get; set; } = 0;
        public string? PaymentTerms { get; set; }
        public string? BankAccount { get; set; }
        public string? IFSC { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
namespace TravelManagement.Core.DTOs
{
    /// <summary>Valid e-mail address, or empty / null.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OptionalEmailAttribute : ValidationAttribute
    {
        private static readonly EmailAddressAttribute Inner = new();

        public OptionalEmailAttribute() : base("The {0} field is not a valid e-mail address.") { }

        public override bool IsValid(object? value) =>
            value is not string s || string.IsNullOrWhiteSpace(s) || Inner.IsValid(s.Trim());
    }
}
