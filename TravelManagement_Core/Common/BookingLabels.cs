using TravelManagement.Core.Models;

namespace TravelManagement.Core.Common
{
    /// <summary>Readable booking type names, shared by invoices, statements and lists.</summary>
    public static class BookingLabels
    {
        public static string TypeName(BookingType type) => type switch
        {
            BookingType.AirportPickup       => "Airport Pickup",
            BookingType.AirportDrop         => "Airport Drop",
            BookingType.RailwayStation      => "Railway Station",
            BookingType.FullDay             => "Full Day",
            BookingType.SightSeeing         => "Sightseeing",
            BookingType.SelfDriveRental     => "Self-Drive Rental",
            BookingType.NorthGoaSightseeing => "North Goa Sightseeing",
            BookingType.SouthGoaSightseeing => "South Goa Sightseeing",
            BookingType.DudhsagarTour       => "Dudhsagar Tour",
            BookingType.Notspecified        => "Not specified",
            _                               => type.ToString(),
        };

        /// <summary>"Full Day (8 hr / 80 km)" — Full Day packages carry hours and km.</summary>
        public static string TypeName(BookingType type, int? fullDayHours) =>
            type == BookingType.FullDay && fullDayHours is 8 or 12
                ? $"Full Day ({fullDayHours} hr / {fullDayHours * 10} km)"
                : TypeName(type);

        /// <summary>"Mopa Airport pickup + North Goa Sightseeing + drop at Dabolim Airport"</summary>
        public static string Label(BookingType type, string? endDrop, string? startPickup = null, int? fullDayHours = null)
        {
            var label = TypeName(type, fullDayHours);
            if (!string.IsNullOrWhiteSpace(startPickup)) label = $"{startPickup.Trim()} pickup + {label}";
            if (!string.IsNullOrWhiteSpace(endDrop)) label = $"{label} + drop at {endDrop.Trim()}";
            return label;
        }

        public static string Label(string type, string? endDrop, string? startPickup = null, int? fullDayHours = null) =>
            Enum.TryParse<BookingType>(type, out var t) ? Label(t, endDrop, startPickup, fullDayHours) : type;
    }
}
