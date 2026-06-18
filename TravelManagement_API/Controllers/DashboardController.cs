using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
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
        private readonly TenantContext _tenant;

        public DashboardController(AppDbContext db, TenantContext tenant)
        {
            _db     = db;
            _tenant = tenant;
        }

        // Org-scoped base queries
        private IQueryable<Booking>       OrgBookings  => _tenant.ShouldFilter ? _db.Bookings.Where(b => b.OrgId == _tenant.OrgId)         : _db.Bookings;
        private IQueryable<Vehicle>       OrgVehicles  => _tenant.ShouldFilter ? _db.Vehicles.Where(v => v.OrgId == _tenant.OrgId)         : _db.Vehicles;
        private IQueryable<User>          OrgUsers     => _tenant.ShouldFilter ? _db.Users.Where(u => u.OrgId == _tenant.OrgId)            : _db.Users;
        private IQueryable<VehicleExpence>OrgExpenses  => _tenant.ShouldFilter ? _db.vehicleExpences.Where(e => e.OrgId == _tenant.OrgId)  : _db.vehicleExpences;
        private IQueryable<Salary>        OrgSalaries  => _tenant.ShouldFilter ? _db.salaries.Where(s => s.OrgId == _tenant.OrgId)         : _db.salaries;

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
            var today       = DateOnly.FromDateTime(DateTime.Today);
            var targetMonth = month ?? today.Month;
            var targetYear  = year  ?? today.Year;

            // KPI aggregates
            var totalBookings = await OrgBookings.AsNoTracking().CountAsync();

            var todayBookings = await OrgBookings.AsNoTracking()
                .CountAsync(b => b.travelDate == today && b.Status != Status.Canceled);

            var todayEarning = (await OrgBookings.AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled)
                .SumAsync(b => (decimal?)b.Amount)) ?? 0;

            var totalRevenue = (await OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled)
                .SumAsync(b => (decimal?)b.Amount)) ?? 0;

            // Per-date lightweight data for the requested month
            var monthRows = await OrgBookings.AsNoTracking()
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
                    vehicleNames = g.Select(b => b.VehicleName ?? "External").Distinct().ToList()
                })
                .OrderBy(x => x.date)
                .ToList();

            // Available resources for today
            var busyVehicleIds = await OrgBookings.AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled && b.VehicleId != null)
                .Select(b => b.VehicleId!.Value)
                .Distinct()
                .ToListAsync();

            var busyDriverIds = await OrgBookings.AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled && b.Userid != null)
                .Select(b => b.Userid!.Value)
                .Distinct()
                .ToListAsync();

            var allVehicles = await OrgVehicles.AsNoTracking()
                .Select(v => new { v.VehicleId, v.VehicleName, v.VehicleNumber, v.VehicleType, v.Seatingcapacity })
                .ToListAsync();

            var availableVehicles = allVehicles.Where(v => !busyVehicleIds.Contains(v.VehicleId)).ToList();

            var allDrivers = await OrgUsers.AsNoTracking()
                .Where(u => u.Status && !u.IsDeleted && u.Licence != null)
                .Select(u => new { u.userId, u.EmployeeName, u.Number })
                .ToListAsync();

            var availableDrivers = allDrivers.Where(d => !busyDriverIds.Contains(d.userId)).ToList();

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

        /// <summary>
        /// Full expense + revenue summary for a given month.
        /// Returns KPI totals, by-category breakdown, salary per driver,
        /// vehicle-wise breakdown, and a 6-month trend.
        /// </summary>
        [HttpGet("expense-summary")]
        public async Task<IActionResult> GetExpenseSummary(
            [FromQuery] int? month,
            [FromQuery] int? year)
        {
            var today = DateTime.Today;
            var m     = month ?? today.Month;
            var y     = year  ?? today.Year;

            // Vehicle expenses (Salary lives in salaries table)
            var vehicleExpList = await OrgExpenses.AsNoTracking()
                .Include(e => e.Vehicle)
                .Where(e => e.ExpenseDate.Month == m
                         && e.ExpenseDate.Year  == y
                         && e.CategoryType      != Category.Salary)
                .ToListAsync();

            var vehicleExpenses = vehicleExpList.Sum(e => e.Amount);

            // Salary expenses: ALL auto-created records for this month
            var salaryList = await OrgSalaries.AsNoTracking()
                .Include(s => s.user)
                .Where(s => s.Month == m && s.Year == y)
                .ToListAsync();

            var salaryExpenses = salaryList.Sum(s => s.NetSalaey);

            // Revenue
            var revenue = (await OrgBookings.AsNoTracking()
                .Where(b => b.Status         != Status.Canceled
                         && b.travelDate.Month == m
                         && b.travelDate.Year  == y)
                .SumAsync(b => (decimal?)b.Amount)) ?? 0m;

            var totalExpenses = vehicleExpenses + salaryExpenses;

            // By-category breakdown
            var byCategory = vehicleExpList
                .GroupBy(e => e.CategoryType.ToString())
                .Select(g => new { category = g.Key, amount = g.Sum(e => e.Amount), count = g.Count() })
                .OrderByDescending(g => g.amount)
                .ToList<object>();

            // Salary details per driver
            var salaryDetails = salaryList
                .Select(s => new
                {
                    salaryId   = s.SalaryId,
                    driverName = s.user?.EmployeeName ?? "Unknown",
                    amount     = s.NetSalaey,
                    isPaid     = s.IsPaid,
                    paidDate   = s.PaidDate,
                    month      = s.Month,
                    year       = s.Year,
                })
                .OrderBy(s => s.driverName)
                .ToList<object>();

            // Vehicle-wise breakdown
            var vehicleBreakdown = vehicleExpList
                .Where(e => e.VehicleID.HasValue)
                .GroupBy(e => new
                {
                    id   = e.VehicleID!.Value,
                    name = e.Vehicle?.VehicleName   ?? "Unknown",
                    num  = e.Vehicle?.VehicleNumber ?? "",
                })
                .Select(g => new
                {
                    vehicleId     = g.Key.id,
                    vehicleName   = g.Key.name,
                    vehicleNumber = g.Key.num,
                    fuel      = g.Where(e => e.CategoryType == Category.Fuel).Sum(e => e.Amount),
                    repair    = g.Where(e => e.CategoryType == Category.Repair).Sum(e => e.Amount),
                    emi       = g.Where(e => e.CategoryType == Category.EMI).Sum(e => e.Amount),
                    service   = g.Where(e => e.CategoryType == Category.Service).Sum(e => e.Amount),
                    insurance = g.Where(e => e.CategoryType == Category.Insurance).Sum(e => e.Amount),
                    tyre      = g.Where(e => e.CategoryType == Category.Tyre).Sum(e => e.Amount),
                    other     = g.Where(e => e.CategoryType == Category.Other
                                          || e.CategoryType == Category.Towing
                                          || e.CategoryType == Category.DocumentRenew).Sum(e => e.Amount),
                    total     = g.Sum(e => e.Amount),
                })
                .OrderByDescending(v => v.total)
                .ToList<object>();

            // Monthly trend: current month + 5 previous months
            var trendStart     = new DateTime(y, m, 1).AddMonths(-5);
            var trendEnd       = new DateTime(y, m, DateTime.DaysInMonth(y, m), 23, 59, 59);
            var trendStartDate = DateOnly.FromDateTime(trendStart);
            var trendEndDate   = DateOnly.FromDateTime(trendEnd);

            var trendBookings = await OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled
                         && b.travelDate >= trendStartDate
                         && b.travelDate <= trendEndDate)
                .GroupBy(b => new { b.travelDate.Year, b.travelDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, revenue = g.Sum(b => (decimal?)b.Amount) ?? 0m })
                .ToListAsync();

            int trendStartYM = trendStart.Year * 100 + trendStart.Month;
            int currentYM    = y * 100 + m;

            var trendVehExp = await OrgExpenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= trendStart
                         && e.ExpenseDate <= trendEnd
                         && e.CategoryType != Category.Salary)
                .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, amount = g.Sum(e => (decimal?)e.Amount) ?? 0m })
                .ToListAsync();

            var trendSalaries = await OrgSalaries.AsNoTracking()
                .Where(s => s.Year * 100 + s.Month >= trendStartYM
                         && s.Year * 100 + s.Month <= currentYM)
                .GroupBy(s => new { s.Year, s.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, amount = g.Sum(s => (decimal?)s.NetSalaey) ?? 0m })
                .ToListAsync();

            var monthlyTrend = Enumerable.Range(0, 6).Select(i =>
            {
                var d  = new DateTime(y, m, 1).AddMonths(-5 + i);
                var bk = trendBookings.FirstOrDefault(x => x.Year  == d.Year && x.Month == d.Month);
                var ex = trendVehExp.FirstOrDefault(x  => x.Year  == d.Year && x.Month == d.Month);
                var sl = trendSalaries.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
                return new
                {
                    month    = d.Month,
                    year     = d.Year,
                    label    = d.ToString("MMM yy"),
                    revenue  = bk?.revenue ?? 0m,
                    expenses = (ex?.amount ?? 0m) + (sl?.amount ?? 0m),
                };
            }).ToList<object>();

            return ApiOk(new
            {
                month          = m,
                year           = y,
                vehicleExpenses,
                salaryExpenses,
                totalExpenses,
                revenue,
                netProfit      = revenue - totalExpenses,
                byCategory,
                salaryDetails,
                vehicleBreakdown,
                monthlyTrend,
            });
        }
    }
}
