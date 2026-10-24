using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// Organisation-level spend that is not tied to a vehicle: advertising (Google Ads,
    /// Meta Ads…), software subscriptions, office, operational and custom categories.
    /// Vehicle running costs stay in VehicleExpence.
    /// </summary>
    public class BusinessExpense
    {
        [Key]
        public int BusinessExpenseId { get; set; }

        public DateOnly ExpenseDate { get; set; }
        public decimal Amount { get; set; }

        /// <summary>Advertising, Marketing, Software, Operational, Driver, Office, Other — or any custom text.</summary>
        [Required]
        public string Category { get; set; } = "Other";

        /// <summary>For advertising: Google Ads, Meta Ads, Instagram, Facebook, Other.</summary>
        public string? Platform { get; set; }
        public string? CampaignName { get; set; }

        public string? Description { get; set; }
        public string? PaymentMethod { get; set; }

        // Soft delete — expense history stays available for reports and the audit log
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }

        public int OrgId { get; set; } = 1;
    }
}
