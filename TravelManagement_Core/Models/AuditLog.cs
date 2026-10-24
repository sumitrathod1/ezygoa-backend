using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>
    /// Who changed what, and when, for financial records. Written automatically by
    /// AuditSaveChangesInterceptor on every save — no code path can forget it.
    /// </summary>
    public class AuditLog
    {
        [Key]
        public long AuditLogId { get; set; }

        public int OrgId { get; set; }

        public int? UserId { get; set; }
        public string? UserName { get; set; }

        /// <summary>Created / Updated / Deleted</summary>
        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }

        /// <summary>Booking this change relates to, when there is one (for booking history).</summary>
        public int? BookingId { get; set; }

        /// <summary>Human-readable one-liner, e.g. "Booking #12 updated — Amount: 3500 → 3600".</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>JSON: { "Field": { "from": x, "to": y } } (or the full values on create/delete).</summary>
        public string? Changes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
