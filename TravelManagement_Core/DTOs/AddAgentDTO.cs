using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.DTOs
{
    public class AddAgentDTO
    {
        public string Name { get; set; } = string.Empty;

        [Phone]
        public string ContactNumber { get; set; } = string.Empty;
        public string AgentType { get; set; } = string.Empty;

        [EmailAddress]
        public string Email { get; set; } = string.Empty;

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