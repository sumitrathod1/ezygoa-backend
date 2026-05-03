using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Customers
    {
        [Key]
        public int CustomersId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
        public string? AlternateNumber { get; set; } = string.Empty;
        public DateOnly TravelDate { get; set; }
    }
}