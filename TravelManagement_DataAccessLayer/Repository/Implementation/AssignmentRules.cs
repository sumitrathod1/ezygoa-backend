using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    /// <summary>
    /// When is a booking "assigned"? Shared by the booking form, the assignment board,
    /// the header badge and transfers so they always agree.
    /// </summary>
    public static class AssignmentRules
    {
        /// <summary>Vehicles with this number prefix are rent-a-car placeholders, not real cars.</summary>
        public const string RentalPlaceholderPrefix = "RENTAL-";

        /// <summary>Self-drive rentals need a car but no driver.</summary>
        public static bool NeedsDriver(Booking b) => b.BookingType != BookingType.SelfDriveRental;

        public static bool IsFullyAssigned(Booking b) =>
            b.ExternalEmployeeId != null || (b.VehicleId != null && (b.Userid != null || !NeedsDriver(b)));

        /// <summary>Pending ⇄ Assigned follows the driver / vehicle. Completed and cancelled are left alone.</summary>
        public static void SyncStatus(Booking b)
        {
            if (b.Status is Status.Completed or Status.Canceled) return;
            var done = IsFullyAssigned(b);
            b.Assigned = done;
            b.Status = done ? Status.Assigned : Status.Pending;
        }

        /// <summary>Today in India — the server may run on UTC, which is still "yesterday" until 5:30 am IST.</summary>
        public static DateOnly TodayInIndia()
        {
            try
            {
                var ist = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ist));
            }
            catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(DateTime.Today); }
        }

        public static bool IsRentalPlaceholder(string? vehicleNumber) =>
            vehicleNumber != null && vehicleNumber.StartsWith(RentalPlaceholderPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
