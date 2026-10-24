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
    [Authorize(Roles = "Admin")]   // was any signed-in user — drivers could read revenue and every salary
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
        /// What the business actually earns from a set of bookings: the full fare for
        /// bookings it runs itself, only the commission for bookings handed to an
        /// external partner (the rest of that fare belongs to the partner).
        /// </summary>
        private async Task<decimal> EarnedAsync(IQueryable<Booking> bookings)
        {
            var own = await bookings.Where(b => b.ExternalEmployeeId == null)
                .SumAsync(b => (decimal?)b.Amount) ?? 0m;
            var partnerIds = bookings.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId);
            var commission = await _db.ExternalEmployeeCashCollections
                .Where(s => partnerIds.Contains(s.BookingId))
                .SumAsync(s => (decimal?)s.CommissionAmount) ?? 0m;
            return own + commission;
        }

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
            var totalBookings = await OrgBookings.AsNoTracking().CountAsync(b => b.Status != Status.Canceled);

            var todayBookings = await OrgBookings.AsNoTracking()
                .CountAsync(b => b.travelDate == today && b.Status != Status.Canceled);

            var todayEarning = await EarnedAsync(OrgBookings.AsNoTracking()
                .Where(b => b.travelDate == today && b.Status != Status.Canceled));

            var totalRevenue = await EarnedAsync(OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled));

            var grossBookingValue = (await OrgBookings.AsNoTracking()
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
                grossBookingValue,
                calendarData,
                availableVehicles,
                availableDrivers
            });
        }

        /// <summary>
        /// Business earnings for a date range (default: this month). Keeps gross booking
        /// value, earned revenue, commission earned / collected / outstanding and each
        /// expense type separate, so the full customer fare of partner bookings is never
        /// counted as income.
        /// </summary>
        [HttpGet("earnings-summary")]
        public async Task<IActionResult> GetEarningsSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var f = from ?? new DateOnly(today.Year, today.Month, 1);
            var t = to ?? f.AddMonths(1).AddDays(-1);
            if (f > t) return ApiBadRequest("'From' date must be before 'To' date");

            var live = OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled && b.travelDate >= f && b.travelDate <= t);

            var gross = await live.SumAsync(b => (decimal?)b.Amount) ?? 0m;
            var own   = await live.Where(b => b.ExternalEmployeeId == null).SumAsync(b => (decimal?)b.Amount) ?? 0m;
            var ownCount = await live.CountAsync(b => b.ExternalEmployeeId == null);

            // Partner bookings in the period with their settlement state
            var partnerRows = await _db.ExternalEmployeeCashCollections.AsNoTracking()
                .Include(s => s.ExternalEmployee)
                .Where(s => live.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId).Contains(s.BookingId))
                .ToListAsync();
            var ids = partnerRows.Select(s => s.BookingId).ToList();
            var advances = await _db.BookingPaymentAllocations.AsNoTracking()
                .Where(p => ids.Contains(p.BookingId) && p.PayerType == PayerType.Customer)
                .GroupBy(p => p.BookingId)
                .Select(g => new { g.Key, Paid = g.Sum(x => x.PaidAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Paid);

            decimal commission = 0, commissionOutstanding = 0, payableOutstanding = 0;
            var partners = new Dictionary<int, (string Name, int Bookings, decimal Commission, decimal Outstanding, decimal WeOwe)>();
            foreach (var s in partnerRows)
            {
                var adv = advances.GetValueOrDefault(s.BookingId, 0);
                var outC = SettlementMath.CommissionOutstanding(s, adv);
                var outP = SettlementMath.PayableOutstanding(s, adv);
                commission += s.CommissionAmount;
                commissionOutstanding += outC;
                payableOutstanding += outP;

                var key = s.ExternalEmployeeId ?? 0;
                var cur = partners.GetValueOrDefault(key, (s.ExternalEmployee?.externalEmployeeName ?? "Unknown partner", 0, 0m, 0m, 0m));
                partners[key] = (cur.Item1, cur.Item2 + 1, cur.Item3 + s.CommissionAmount, cur.Item4 + outC, cur.Item5 + outP);
            }

            // Expenses in the period
            var fDt = f.ToDateTime(TimeOnly.MinValue);
            var tDt = t.ToDateTime(TimeOnly.MaxValue);
            var vehicleExp = await OrgExpenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= fDt && e.ExpenseDate <= tDt && e.CategoryType != Category.Salary)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;
            int fYM = f.Year * 100 + f.Month, tYM = t.Year * 100 + t.Month;
            var salaries = await OrgSalaries.AsNoTracking()
                .Where(s => s.Year * 100 + s.Month >= fYM && s.Year * 100 + s.Month <= tYM)
                .SumAsync(s => (decimal?)s.NetSalaey) ?? 0m;

            var business = await _db.BusinessExpenses.AsNoTracking()
                .Where(e => !e.IsDeleted && (!_tenant.ShouldFilter || e.OrgId == _tenant.OrgId)
                         && e.ExpenseDate >= f && e.ExpenseDate <= t)
                .ToListAsync();
            bool IsAd(BusinessExpense e) => e.Platform != null || e.Category.Equals("Advertising", StringComparison.OrdinalIgnoreCase);
            var adSpend    = business.Where(IsAd).Sum(e => e.Amount);
            var otherBiz   = business.Where(e => !IsAd(e)).Sum(e => e.Amount);

            var earned        = own + commission;
            var totalExpenses = vehicleExp + salaries + adSpend + otherBiz;

            return ApiOk(new
            {
                from = f,
                to   = t,
                // Revenue — kept separate on purpose
                grossBookingValue   = gross,
                ownBookingRevenue   = own,
                ownBookings         = ownCount,
                partnerBookings     = partnerRows.Count,
                commissionEarned    = commission,
                commissionCollected = commission - commissionOutstanding,
                commissionOutstanding,
                payableToPartners   = payableOutstanding,
                earnedRevenue       = earned,
                // Costs
                expenses = new
                {
                    vehicle     = vehicleExp,
                    salaries,
                    advertising = adSpend,
                    business    = otherBiz,
                    total       = totalExpenses,
                    byCategory  = business.GroupBy(e => IsAd(e) ? "Advertising" : e.Category)
                        .Select(g => new { category = g.Key, amount = g.Sum(e => e.Amount) })
                        .OrderByDescending(x => x.amount),
                },
                advertising = new
                {
                    total      = adSpend,
                    byPlatform = business.Where(IsAd).GroupBy(e => e.Platform ?? "Unspecified")
                        .Select(g => new { platform = g.Key, amount = g.Sum(e => e.Amount) })
                        .OrderByDescending(x => x.amount),
                    byCampaign = business.Where(e => IsAd(e) && e.CampaignName != null)
                        .GroupBy(e => new { e.CampaignName, e.Platform })
                        .Select(g => new { campaign = g.Key.CampaignName, platform = g.Key.Platform, amount = g.Sum(e => e.Amount) })
                        .OrderByDescending(x => x.amount),
                    // Overall only: bookings aren't tagged with the campaign that produced them
                    commissionPerRupeeSpent = adSpend > 0 ? Math.Round(commission / adSpend, 2) : (decimal?)null,
                    earnedPerRupeeSpent     = adSpend > 0 ? Math.Round(earned / adSpend, 2) : (decimal?)null,
                    adSpendShareOfEarned    = earned > 0 ? Math.Round(adSpend / earned * 100, 1) : (decimal?)null,
                    isEstimate = true,
                    note = "Overall ratio for the period. Bookings are not linked to the ad campaign that produced them, so per-campaign return can't be measured.",
                },
                netEarnings = earned - totalExpenses,
                partners = partners.Select(p => new
                {
                    partnerId   = p.Key,
                    name        = p.Value.Name,
                    bookings    = p.Value.Bookings,
                    commission  = p.Value.Commission,
                    collected   = p.Value.Commission - p.Value.Outstanding,
                    outstanding = p.Value.Outstanding,
                    weOwe       = p.Value.WeOwe,
                }).OrderByDescending(p => p.commission),
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

            // Revenue = earned (commission only on partner bookings); gross kept separate
            var monthBookings = OrgBookings.AsNoTracking()
                .Where(b => b.Status         != Status.Canceled
                         && b.travelDate.Month == m
                         && b.travelDate.Year  == y);
            var revenue = await EarnedAsync(monthBookings);
            var grossBookingValue = (await monthBookings.SumAsync(b => (decimal?)b.Amount)) ?? 0m;

            // Business / advertising spend (organisation-level, not per vehicle)
            var monthStart = new DateOnly(y, m, 1);
            var monthEnd   = monthStart.AddMonths(1).AddDays(-1);
            var bizList = await _db.BusinessExpenses.AsNoTracking()
                .Where(e => !e.IsDeleted && (!_tenant.ShouldFilter || e.OrgId == _tenant.OrgId)
                         && e.ExpenseDate >= monthStart && e.ExpenseDate <= monthEnd)
                .Select(e => new { e.Amount, e.Platform, e.Category })
                .ToListAsync();
            var businessExpenses = bizList.Sum(e => e.Amount);
            var advertisingSpend = bizList.Where(e => e.Platform != null || e.Category == "Advertising").Sum(e => e.Amount);

            var totalExpenses = vehicleExpenses + salaryExpenses + businessExpenses;

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

            var trendScope = OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled
                         && b.travelDate >= trendStartDate
                         && b.travelDate <= trendEndDate);

            var trendOwn = await trendScope
                .Where(b => b.ExternalEmployeeId == null)
                .GroupBy(b => new { b.travelDate.Year, b.travelDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, amount = g.Sum(b => (decimal?)b.Amount) ?? 0m })
                .ToListAsync();

            var trendCommission = await _db.ExternalEmployeeCashCollections
                .Where(s => trendScope.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId).Contains(s.BookingId))
                .GroupBy(s => new { s.Booking.travelDate.Year, s.Booking.travelDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, amount = g.Sum(s => (decimal?)s.CommissionAmount) ?? 0m })
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

            var trendBiz = await _db.BusinessExpenses.AsNoTracking()
                .Where(e => !e.IsDeleted && (!_tenant.ShouldFilter || e.OrgId == _tenant.OrgId)
                         && e.ExpenseDate >= trendStartDate && e.ExpenseDate <= trendEndDate)
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
                var own = trendOwn.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.amount ?? 0m;
                var com = trendCommission.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.amount ?? 0m;
                var ex = trendVehExp.FirstOrDefault(x  => x.Year  == d.Year && x.Month == d.Month);
                var sl = trendSalaries.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
                var bz = trendBiz.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
                return new
                {
                    month    = d.Month,
                    year     = d.Year,
                    label    = d.ToString("MMM yy"),
                    revenue  = own + com,
                    expenses = (ex?.amount ?? 0m) + (sl?.amount ?? 0m) + (bz?.amount ?? 0m),
                };
            }).ToList<object>();

            return ApiOk(new
            {
                month          = m,
                year           = y,
                vehicleExpenses,
                salaryExpenses,
                businessExpenses,
                advertisingSpend,
                totalExpenses,
                revenue,
                grossBookingValue,
                netProfit      = revenue - totalExpenses,
                byCategory,
                salaryDetails,
                vehicleBreakdown,
                monthlyTrend,
            });
        }
    }
}
