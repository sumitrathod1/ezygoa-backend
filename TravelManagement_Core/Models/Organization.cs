using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// Represents a customer organisation (tenant) using the platform.
    /// OrgId = 0 is reserved for the SuperAdmin system account.
    /// </summary>
    public class Organization
    {
        [Key]
        public int OrgId { get; set; }

        [Required]
        public required string Name { get; set; }

        /// <summary>Short code shown in UI, e.g. "EZYGOA"</summary>
        public string Code { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? Email   { get; set; }
        public string? Phone   { get; set; }
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
    }
}
