using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }
        public int? UserId { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }
}