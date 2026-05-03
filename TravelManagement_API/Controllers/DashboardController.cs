using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [EnableRateLimiting("api")]
    public class DashboardController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db) => _db = db;

        /// <summary>
        /// Lightweight calendar summary for a given month.
        /// Returns KPIs + per-date {count, vehicleNames} — no full booking objects.
        /// ~5 KB response vs 200+ KB from View-Bookings.
        /// </summary>
        [HttpGet("calendar-summary")]
        public async Task<IActionResult> GetCalendarSummary(
            [FromQuery] int? month,
            [FromQuery] int? year)
        {
            var today        = DateOnly.FromDateTime(DateTime.Today);
            var targetMonth  = month ?? today.Month;
            var targetYear   = year  ?? today.Year;

            // KPI aggregates — sequential, DbContext is not thread-safe
            var totalBookings = await _db.Bookings
                .AsNoTracking()
                .CountAsync();

            var todayBookings = await _db.Bookings
                .AsNoTracking()
                .CountAsync(b => b.travelDate == today && b.Status != Status.Canceled);

            var todayEarning = (await _db.Bookings
                .AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled)
                .SumAsync(b => (decimal?)b.Amount)) ?? 0;

            var totalRevenue = (await _db.Bookings
                .AsNoTracking()
                .Where(b => b.Status != Status.Canceled)
                .SumAsync(b => (decimal?)b.Amount)) ?? 0;

            // Per-date lightweight data for the requested month
            // EF Core projects a LEFT JOIN on Vehicle automatically — no Include needed
            var monthRows = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.Status != Status.Canceled
                         && b.travelDate.Month == targetMonth
                         && b.travelDate.Year  == targetYear)
                .Select(b => new
                {
                    b.travelDate,
                    VehicleName = b.Vehicle != null ? b.Vehicle.VehicleName : null
                })
                .ToListAsync();

            var calendarData = monthRows
                .GroupBy(b => b.travelDate)
                .Select(g => new
                {
                    date         = g.Key.ToString("yyyy-MM-dd"),
                    count        = g.Count(),
                    vehicleNames = g.Select(b => b.VehicleName ?? "External")
                                    .Distinct()
                                    .ToList()
                })
                .OrderBy(x => x.date)
                .ToList();

            // Available resources for today
            var busyVehicleIds = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled && b.VehicleId != null)
                .Select(b => b.VehicleId!.Value)
                .Distinct()
                .ToListAsync();

            var busyDriverIds = await _db.Bookings
                .AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled && b.Userid != null)
                .Select(b => b.Userid!.Value)
                .Distinct()
                .ToListAsync();

            var allVehicles = await _db.Vehicles
                .AsNoTracking()
                .Select(v => new { v.VehicleId, v.VehicleName, v.VehicleNumber, v.VehicleType, v.Seatingcapacity })
                .ToListAsync();

            var availableVehicles = allVehicles
                .Where(v => !busyVehicleIds.Contains(v.VehicleId))
                .ToList();

            var allDrivers = await _db.Users
                .AsNoTracking()
                .Where(u => u.Status && !u.IsDeleted && u.Licence != null)
                .Select(u => new { u.userId, u.EmployeeName, u.Number })
                .ToListAsync();

            var availableDrivers = allDrivers
                .Where(d => !busyDriverIds.Contains(d.userId))
                .ToList();

            return ApiOk(new
            {
                totalBookings,
                todayBookings,
                todayEarning,
                totalRevenue,
                calendarData,
                availableVehicles,
                availableDrivers
            });
        }
    }
}
