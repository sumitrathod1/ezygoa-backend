using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class OvertimeLog
    {
        [Key]
        public int OvertimeID { get; set; }
        public decimal hours { get; set; }
        public required string Description { get; set; }
        public DateTime Date { get; set; }
        public bool IsApproved { get; set; }
        public int userId { get; set; }
        public User? user { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
    }
}