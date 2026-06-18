using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public class Documents
    {
        [Key]
        public int DocumentID { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool HasExpiry { get; set; } = true;
        public int? VehicleID { get; set; }
        public Vehicle? Vehicle { get; set; }

        // New fields
        public string? DocumentType { get; set; }
        public string? Category { get; set; }       // Vehicle | Driver | Company
        public string? DocumentNumber { get; set; }
        public string? IssuedBy { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? DriverName { get; set; }     // For Driver category docs
        public string? FileUrl { get; set; }

        // Multi-tenancy
        public int OrgId { get; set; } = 1;
    }
}
