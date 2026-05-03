namespace TravelManagement.Core.DTOs
{
    public class AddAgentPaymentDto
    {
        public int AgentId { get; set; }
        public decimal TotalPaidAmount { get; set; }
    }
}