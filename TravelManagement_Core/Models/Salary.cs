using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Salary
    {
        [Key]
        public int SalaryId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal BaseSalay { get; set; }
        public decimal Deduction { get; set; }
        public decimal Overtimepay { get; set; }
        public decimal NetSalaey { get; set; }
        public int userID { get; set; }
        public User? user { get; set; }
        public bool IsPaid { get; set; } = false;
        public DateTime? PaidDate { get; set; }
        public string? Notes { get; set; }

        // Multi-tenancy
        public int OrgId { get; set; } = 1;
    }
}