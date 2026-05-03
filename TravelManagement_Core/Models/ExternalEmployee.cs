using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class ExternalEmployee
    {
        [Key]
        public int externalEmployeeID { get; set; }
        public string? externalEmployeeName { get; set; }
        public string? externalEmployeeNumber { get; set; }
    }
}