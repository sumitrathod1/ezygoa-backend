namespace TravelManagement.Core.Models
{
    public class AgentCashCollection
    {
        public int Id { get; set; }
        public int AgentId { get; set; }
        public TravelAgent Agent { get; set; } = null!;
        public decimal AmountCollected { get; set; }
        public DateTime CollectionDate { get; set; }
    }
}