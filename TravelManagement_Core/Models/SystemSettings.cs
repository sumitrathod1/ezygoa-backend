using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    /// <summary>App-wide settings the super admin can change. Always a single row (Id = 1).</summary>
    public class SystemSettings
    {
        public const int DefaultSessionDays = 30;
        public const int MaxSessionDays = 365;

        [Key]
        public int Id { get; set; } = 1;

        /// <summary>How long a sign-in lasts before the user has to log in again.</summary>
        public int SessionDays { get; set; } = DefaultSessionDays;
    }
}
