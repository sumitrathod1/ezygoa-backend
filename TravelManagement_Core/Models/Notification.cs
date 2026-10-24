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

        // Multi-tenancy (existing rows belong to EzyGoa = 1, the only mailbox so far)
        public int OrgId { get; set; } = 1;
    }
}