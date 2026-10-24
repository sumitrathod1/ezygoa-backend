using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// Two organisations that may hand bookings to each other (e.g. EzyGoa ↔ ZipGoa).
    /// Set up by the SuperAdmin only, so no tenant can push customer data to a company
    /// it has no arrangement with. Stored once per pair with OrgAId &lt; OrgBId.
    /// </summary>
    public class OrgLink
    {
        [Key]
        public int OrgLinkId { get; set; }
        public int OrgAId { get; set; }
        public int OrgBId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }
    }
}
