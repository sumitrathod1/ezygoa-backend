using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum Category
    {
        Repair,
        Fuel,
        Towing,
        DocumentRenew,
        Salary,
        EMI,
        Insurance,
        Service,
        Other,
        Tyre
    }

    public class VehicleExpence
    {
        [Key]
        public int VehicleExpenceId { get; set; }
        public DateTime ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public Category CategoryType { get; set; }
        public int? VehicleID { get; set; }
        public string? Notes { get; set; }
        public Vehicle? Vehicle { get; set; }
    }
}