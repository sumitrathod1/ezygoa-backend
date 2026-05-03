using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class EmailInquiry
    {
        [Key]
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public DateOnly? TravelDate { get; set; }
        public int Pax { get; set; }
        public string? VehicleName { get; set; }
        public bool IsConfirmed { get; set; } = false;
        public bool IsRejected { get; set; } = false;
        public DateOnly? CreatedAt { get; set; }
    }
}