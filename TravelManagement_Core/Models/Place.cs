using System.ComponentModel.DataAnnotations;

namespace TravelManagement.Core.Models
{
    public enum PlaceZone
    {
        North = 0,
        South = 1,
        Hub   = 2
    }

    public class Place
    {
        [Key]
        public int PlaceId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Aliases { get; set; }

        public PlaceZone Zone { get; set; }

        public bool PickupAllowed { get; set; } = true;

        public bool DropAllowed { get; set; } = true;

        public string? RestrictionMessage { get; set; }

        public bool IsActive { get; set; } = true;

        public int OrgId { get; set; } = 1;
    }
}
