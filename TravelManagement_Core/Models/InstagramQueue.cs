using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class InstagramQueue
    {
        [Key]
        public int QueueId { get; set; }

        public string ImageUrl { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;

        // pending | posted | failed
        public string Status { get; set; } = "pending";

        public DateTime? PostedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int OrgId { get; set; } = 1;
    }
}
