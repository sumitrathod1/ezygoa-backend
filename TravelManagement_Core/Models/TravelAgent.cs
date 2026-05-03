using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum TravelAgentType
    {
        Agent,
        TravelOwner,
        Hotel,
        TourOperator,
        TravelAgency,
        OnlinePlatform,
        Individual
    }

    public class TravelAgent
    {
        [Key]
        public int AgentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TravelAgentType type { get; set; }
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public decimal? CommissionRate { get; set; }

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