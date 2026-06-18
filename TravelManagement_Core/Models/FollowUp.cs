using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class FollowUp
    {
        [Key]
        public int FollowUpId { get; set; }
        public int? EmailInquiryId { get; set; }
        public EmailInquiry? EmailInquiry { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime FollowUpDate { get; set; } = DateTime.UtcNow;
        public DateTime? NextFollowUpAt { get; set; }
        public int? CreatedByUserId { get; set; }
        public User? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int OrgId { get; set; } = 1;
    }
}
