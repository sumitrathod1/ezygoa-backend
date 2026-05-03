using TravelManagement.Core.Models;

namespace TravelManagement.Core.DTOs
{
    public class AgentDashboardDTO
    {
        public int AgentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public TravelAgentType type { get; set; }
        public int BookingCount { get; set; }
        public decimal Earned { get; set; }
        public decimal Pending { get; set; }

        // Contact & profile fields for card display
        public string? ContactNumber { get; set; }
        public string? Email { get; set; }
        public string? ContactPerson { get; set; }
        public string? WhatsApp { get; set; }
        public string? Address { get; set; }
        public decimal CommissionPercent { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; }
    }
}