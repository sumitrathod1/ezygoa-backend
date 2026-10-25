using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.DataAccessLayer.Repository.Implementation;
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
            [FromQuery] int? year,
            [FromQuery] bool calendarOnly = false)
        {
            // The database is remote, so every query costs a network round trip: this used to
            // run ~12 one after another. Now: 1 query for the calendar (+4 for the KPIs).
            var today       = AssignmentRules.TodayInIndia();
            var targetMonth = month ?? today.Month;
            var targetYear  = year  ?? today.Year;
            var monthStart  = new DateOnly(targetYear, Math.Clamp(targetMonth, 1, 12), 1);
            var monthEnd    = monthStart.AddMonths(1).AddDays(-1);

            // This month's own-fleet bookings and today's (for busy cars / drivers) in one go.
            // Calendar and its counts show own-fleet bookings only; partner bookings live in "Extra".
            var rows = await OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled
                         && ((b.travelDate >= monthStart && b.travelDate <= monthEnd) || b.travelDate == today))
                .Select(b => new
                {
                    b.travelDate, b.ExternalEmployeeId, b.VehicleId, b.Userid,
                    VehicleName = b.Vehicle != null ? b.Vehicle.VehicleName : null
                })
                .ToListAsync();

            var calendarData = rows
                .Where(b => b.ExternalEmployeeId == null && b.travelDate >= monthStart && b.travelDate <= monthEnd)
                .GroupBy(b => b.travelDate)
                .Select(g => new
                {
                    date         = g.Key.ToString("yyyy-MM-dd"),
                    count        = g.Count(),
                    vehicleNames = g.Select(b => b.VehicleName ?? "Not assigned").Distinct().ToList()
                })
                .OrderBy(x => x.date)
                .ToList();

            // Another month of the calendar only (swiping months / prefetch): the KPIs don't change
            if (calendarOnly)
                return ApiOk(new { calendarData });

            // All-time and today's KPIs in one aggregate query
            var kpi = await OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled)
                .GroupBy(b => 1)
                .Select(g => new
                {
                    Total      = g.Count(),
                    Today      = g.Count(b => b.travelDate == today && b.ExternalEmployeeId == null),
                    Gross      = g.Sum(b => b.Amount),
                    OwnAll     = g.Sum(b => b.ExternalEmployeeId == null ? b.Amount : 0m),
                    OwnToday   = g.Sum(b => b.ExternalEmployeeId == null && b.travelDate == today ? b.Amount : 0m),
                })
                .FirstOrDefaultAsync();

            // Partner bookings earn their commission only (all time and today), in one query
            var partnerIds = OrgBookings.Where(b => b.Status != Status.Canceled && b.ExternalEmployeeId != null).Select(b => b.BookingId);
            var commission = await _db.ExternalEmployeeCashCollections.AsNoTracking()
                .Where(s => partnerIds.Contains(s.BookingId))
                .GroupBy(s => 1)
                .Select(g => new
                {
                    All   = g.Sum(s => s.CommissionAmount),
                    Today = g.Sum(s => s.Booking.travelDate == today ? s.CommissionAmount : 0m),
                })
                .FirstOrDefaultAsync();

            var totalBookings     = kpi?.Total ?? 0;
            var todayBookings     = kpi?.Today ?? 0;
            var grossBookingValue = kpi?.Gross ?? 0m;
            var totalRevenue      = (kpi?.OwnAll ?? 0m) + (commission?.All ?? 0m);
            var todayEarning      = (kpi?.OwnToday ?? 0m) + (commission?.Today ?? 0m);

            // Available resources for today
            var todayRows      = rows.Where(b => b.travelDate == today).ToList();
            var busyVehicleIds = todayRows.Where(b => b.VehicleId != null).Select(b => b.VehicleId!.Value).ToHashSet();
            var busyDriverIds  = todayRows.Where(b => b.Userid != null).Select(b => b.Userid!.Value).ToHashSet();

            var allVehicles = (await OrgVehicles.AsNoTracking()
                .Select(v => new { v.VehicleId, v.VehicleName, v.VehicleNumber, v.VehicleType, v.Seatingcapacity })
                .ToListAsync())
                // Rent-a-car placeholders are not cars on the road
                .Where(v => !AssignmentRules.IsRentalPlaceholder(v.VehicleNumber))
                .ToList();

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
            // The database is remote: one query per source covering the whole 6-month trend
            // window (this month included), then everything is worked out in memory.
            // Was ~11 queries one after another.
            var today      = AssignmentRules.TodayInIndia();
            var m          = month ?? today.Month;
            var y          = year  ?? today.Year;
            var monthStart = new DateOnly(y, Math.Clamp(m, 1, 12), 1);
            var monthEnd   = monthStart.AddMonths(1).AddDays(-1);
            var trendStart = monthStart.AddMonths(-5);
            int trendStartYM = trendStart.Year * 100 + trendStart.Month;
            int currentYM    = y * 100 + m;
            var fromDt = trendStart.ToDateTime(TimeOnly.MinValue);
            var toDt   = monthEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // 1. Vehicle expenses (salaries live in the salaries table)
            var vehExp = await OrgExpenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= fromDt && e.ExpenseDate < toDt && e.CategoryType != Category.Salary)
                .Select(e => new
                {
                    e.ExpenseDate, e.Amount, e.CategoryType, e.VehicleID,
                    VehicleName   = e.Vehicle != null ? e.Vehicle.VehicleName : null,
                    VehicleNumber = e.Vehicle != null ? e.Vehicle.VehicleNumber : null,
                })
                .ToListAsync();

            // 2. Salaries (auto-created records per month)
            var salaries = await OrgSalaries.AsNoTracking()
                .Where(s => s.Year * 100 + s.Month >= trendStartYM && s.Year * 100 + s.Month <= currentYM)
                .Select(s => new
                {
                    s.SalaryId, s.Year, s.Month, s.NetSalaey, s.IsPaid, s.PaidDate,
                    DriverName = s.user != null ? s.user.EmployeeName : null,
                })
                .ToListAsync();

            // 3. Business / advertising spend (organisation-level, not per vehicle)
            var biz = await _db.BusinessExpenses.AsNoTracking()
                .Where(e => !e.IsDeleted && (!_tenant.ShouldFilter || e.OrgId == _tenant.OrgId)
                         && e.ExpenseDate >= trendStart && e.ExpenseDate <= monthEnd)
                .Select(e => new { e.ExpenseDate, e.Amount, e.Platform, e.Category })
                .ToListAsync();

            // 4. Bookings per month: own fares and gross value; 5. partner commission per month.
            // Revenue = earned (commission only on partner bookings); gross kept separate.
            var bookingScope = OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled && b.travelDate >= trendStart && b.travelDate <= monthEnd);
            var bookingMonths = await bookingScope
                .GroupBy(b => new { b.travelDate.Year, b.travelDate.Month })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month,
                    Own   = g.Sum(b => b.ExternalEmployeeId == null ? b.Amount : 0m),
                    Gross = g.Sum(b => b.Amount),
                })
                .ToListAsync();
            var partnerIds = bookingScope.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId);
            var commissionMonths = await _db.ExternalEmployeeCashCollections.AsNoTracking()
                .Where(s => partnerIds.Contains(s.BookingId))
                .GroupBy(s => new { s.Booking.travelDate.Year, s.Booking.travelDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(s => s.CommissionAmount) })
                .ToListAsync();

            // ── This month ──
            var vehicleExpList   = vehExp.Where(e => e.ExpenseDate.Year == y && e.ExpenseDate.Month == m).ToList();
            var vehicleExpenses  = vehicleExpList.Sum(e => e.Amount);
            var salaryList       = salaries.Where(s => s.Year == y && s.Month == m).ToList();
            var salaryExpenses   = salaryList.Sum(s => s.NetSalaey);
            var bizList          = biz.Where(e => e.ExpenseDate.Year == y && e.ExpenseDate.Month == m).ToList();
            var businessExpenses = bizList.Sum(e => e.Amount);
            var advertisingSpend = bizList.Where(e => e.Platform != null || e.Category == "Advertising").Sum(e => e.Amount);
            var totalExpenses    = vehicleExpenses + salaryExpenses + businessExpenses;

            decimal Earned(int yy, int mm) =>
                (bookingMonths.FirstOrDefault(x => x.Year == yy && x.Month == mm)?.Own ?? 0m)
                + (commissionMonths.FirstOrDefault(x => x.Year == yy && x.Month == mm)?.Amount ?? 0m);
            var revenue           = Earned(y, m);
            var grossBookingValue = bookingMonths.FirstOrDefault(x => x.Year == y && x.Month == m)?.Gross ?? 0m;

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
                    driverName = s.DriverName ?? "Unknown",
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
                .GroupBy(e => new { id = e.VehicleID!.Value, name = e.VehicleName ?? "Unknown", num = e.VehicleNumber ?? "" })
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
            var monthlyTrend = Enumerable.Range(0, 6).Select(i =>
            {
                var d = monthStart.AddMonths(-5 + i);
                var spent = vehExp.Where(e => e.ExpenseDate.Year == d.Year && e.ExpenseDate.Month == d.Month).Sum(e => e.Amount)
                          + salaries.Where(s => s.Year == d.Year && s.Month == d.Month).Sum(s => s.NetSalaey)
                          + biz.Where(e => e.ExpenseDate.Year == d.Year && e.ExpenseDate.Month == d.Month).Sum(e => e.Amount);
                return new
                {
                    month    = d.Month,
                    year     = d.Year,
                    label    = d.ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture),
                    revenue  = Earned(d.Year, d.Month),
                    expenses = spent,
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

        /// <summary>
        /// Bookings screen: how many bookings, what they earn, cancellations, returning
        /// customers, the week ahead, booking types, where bookings come from, busiest
        /// weekday and a 12-month trend. Period defaults to this month so far.
        /// </summary>
        [HttpGet("bookings-overview")]
        public async Task<IActionResult> GetBookingsOverview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var today = AssignmentRules.TodayInIndia();
            var start = from ?? new DateOnly(today.Year, today.Month, 1);
            var end   = to ?? today;
            if (end < start) return ApiBadRequest("'From' date must be before 'To' date");
            if (end.DayNumber - start.DayNumber > 3700) return ApiBadRequest("Please choose a period of up to 10 years");
            var length    = end.DayNumber - start.DayNumber + 1;
            var prevEnd   = start.AddDays(-1);
            var prevStart = prevEnd.AddDays(-(length - 1));

            var inPeriod = OrgBookings.AsNoTracking().Where(b => b.travelDate >= start && b.travelDate <= end);
            var live     = inPeriod.Where(b => b.Status != Status.Canceled);
            var prevLive = OrgBookings.AsNoTracking().Where(b => b.Status != Status.Canceled && b.travelDate >= prevStart && b.travelDate <= prevEnd);

            var rows = await live
                .Select(b => new
                {
                    b.BookingType, b.Amount, b.travelDate, b.CustomerID, b.Status, b.ExternalEmployeeId, b.TransferredFromOrgId,
                    Agent = b.TravelAgent != null ? b.TravelAgent.Name : null,
                })
                .ToListAsync();
            var cancelled = await inPeriod.CountAsync(b => b.Status == Status.Canceled);
            var earned     = await EarnedAsync(live);

            // Partner bookings earn only their commission; ones with none recorded add ₹0 —
            // shown so a low "Earned" next to many bookings explains itself
            var commissionByBooking = await _db.ExternalEmployeeCashCollections.AsNoTracking()
                .Where(s => live.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId).Contains(s.BookingId))
                .GroupBy(s => s.BookingId)
                .Select(g => new { g.Key, Amount = g.Sum(s => s.CommissionAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Amount);
            var partnerRows = await live.Where(b => b.ExternalEmployeeId != null)
                .Select(b => new { b.BookingId, b.Amount }).ToListAsync();
            var partnerNoCommission = partnerRows.Where(p => commissionByBooking.GetValueOrDefault(p.BookingId) <= 0).ToList();
            var prevCount  = await prevLive.CountAsync();
            var prevEarned = await EarnedAsync(prevLive);

            // Returning = had a booking before this period
            var customerIds = rows.Select(r => r.CustomerID).Distinct().ToList();
            var returning = await OrgBookings.AsNoTracking()
                .Where(b => customerIds.Contains(b.CustomerID) && b.travelDate < start && b.Status != Status.Canceled)
                .Select(b => b.CustomerID).Distinct().CountAsync();

            // The week ahead (whatever the period)
            var weekEnd = today.AddDays(6);
            var ahead = await OrgBookings.AsNoTracking()
                .Where(b => b.Status != Status.Canceled && b.travelDate >= today && b.travelDate <= weekEnd)
                .Select(b => new { b.travelDate, b.ExternalEmployeeId, b.VehicleId, b.Userid, b.BookingType })
                .ToListAsync();
            var nextDays = Enumerable.Range(0, 7).Select(i =>
            {
                var d = today.AddDays(i);
                var list = ahead.Where(a => a.travelDate == d).ToList();
                return new
                {
                    Date  = d,
                    Count = list.Count,
                    // Still needs a car / driver (same rule as the Assign screen)
                    Open  = list.Count(a => a.ExternalEmployeeId == null
                        && (a.VehicleId == null || (a.Userid == null && a.BookingType != BookingType.SelfDriveRental))),
                };
            }).ToList();

            // 12-month trend ending with the period's last month
            var trendStart = new DateOnly(end.Year, end.Month, 1).AddMonths(-11);
            var trendEnd   = new DateOnly(end.Year, end.Month, 1).AddMonths(1).AddDays(-1);
            var trendLive  = OrgBookings.AsNoTracking().Where(b => b.Status != Status.Canceled && b.travelDate >= trendStart && b.travelDate <= trendEnd);
            var trendOwn = await trendLive
                .GroupBy(b => new { b.travelDate.Year, b.travelDate.Month })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month,
                    Count = g.Count(),
                    Own   = g.Where(b => b.ExternalEmployeeId == null).Sum(b => (decimal?)b.Amount) ?? 0m,
                })
                .ToListAsync();
            var trendCommission = await _db.ExternalEmployeeCashCollections
                .Where(s => trendLive.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId).Contains(s.BookingId))
                .GroupBy(s => new { s.Booking.travelDate.Year, s.Booking.travelDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(s => (decimal?)s.CommissionAmount) ?? 0m })
                .ToListAsync();
            var trend = Enumerable.Range(0, 12).Select(i =>
            {
                var m = trendStart.AddMonths(i);
                var o = trendOwn.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                var c = trendCommission.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                return new
                {
                    Label  = m.ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture),
                    m.Year, m.Month,
                    Count  = o?.Count ?? 0,
                    Earned = (o?.Own ?? 0m) + (c?.Amount ?? 0m),
                };
            }).ToList();

            decimal Pct(decimal now, decimal before) => before == 0 ? 0 : Math.Round(100 * (now - before) / Math.Abs(before));
            var gross = rows.Sum(r => r.Amount);
            var weekdayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            var byWeekday = Enumerable.Range(0, 7)
                .Select(d => new { Day = weekdayNames[d], Count = rows.Count(r => (int)r.travelDate.DayOfWeek == d) })
                .ToList();

            return ApiOk(new
            {
                From = start, To = end, Days = length,
                Totals = new
                {
                    Bookings   = rows.Count,
                    Earned     = earned,
                    Gross      = gross,
                    AvgFare    = rows.Count == 0 ? 0 : Math.Round(gross / rows.Count),
                    Completed  = rows.Count(r => r.Status == Status.Completed),
                    Upcoming   = rows.Count(r => r.travelDate >= today),
                    Cancelled  = cancelled,
                    CancelRate = rows.Count + cancelled == 0 ? 0 : Math.Round(100.0 * cancelled / (rows.Count + cancelled)),
                    Customers  = customerIds.Count,
                    Returning  = returning,
                    ReturningPct = customerIds.Count == 0 ? 0 : Math.Round(100.0 * returning / customerIds.Count),
                    PartnerBookings = rows.Count(r => r.ExternalEmployeeId != null),
                    // Fares of partner bookings (the partner's money), and our commission on them
                    PartnerGross      = partnerRows.Sum(p => p.Amount),
                    PartnerCommission = commissionByBooking.Values.Sum(),
                    // Partner bookings with no commission recorded — they add ₹0 to Earned
                    PartnerNoCommission      = partnerNoCommission.Count,
                    PartnerNoCommissionValue = partnerNoCommission.Sum(p => p.Amount),
                    OwnBookings     = rows.Count(r => r.ExternalEmployeeId == null),
                    TransferredIn   = rows.Count(r => r.TransferredFromOrgId != null),
                    Previous = new { Bookings = prevCount, Earned = prevEarned },
                    Change   = new { Bookings = Pct(rows.Count, prevCount), Earned = Pct(earned, prevEarned) },
                },
                NextDays = nextDays,
                ByType = rows.GroupBy(r => r.BookingType.ToString())
                    .Select(g => new { Type = g.Key, Count = g.Count(), Amount = g.Sum(r => r.Amount) })
                    .OrderByDescending(g => g.Count).ToList(),
                // Where bookings come from: each travel agent / hotel, or direct
                BySource = rows.GroupBy(r => r.Agent ?? "Direct")
                    .Select(g => new { Source = g.Key, Count = g.Count(), Amount = g.Sum(r => r.Amount) })
                    .OrderByDescending(g => g.Count).Take(8).ToList(),
                ByWeekday = byWeekday,
                BusiestDay = rows.Count == 0 ? null : byWeekday.OrderByDescending(d => d.Count).First().Day,
                Trend = trend,
            });
        }

        /// <summary>
        /// Fleet KPIs for the Vehicles screens, per vehicle and in total, for a period
        /// (default: this month so far). Earnings = fares of our own cars' bookings
        /// (not cancelled, not handed to a partner); expenses = vehicle expenses except
        /// salaries. Compared with the same-length period just before.
        /// </summary>
        [HttpGet("fleet")]
        public async Task<IActionResult> GetFleet([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? vehicleId)
        {
            var today = AssignmentRules.TodayInIndia();
            var start = from ?? new DateOnly(today.Year, today.Month, 1);
            var end   = to ?? today;
            if (end < start) return ApiBadRequest("'From' date must be before 'To' date");
            if (end.DayNumber - start.DayNumber > 3700) return ApiBadRequest("Please choose a period of up to 10 years");

            var length    = end.DayNumber - start.DayNumber + 1;
            var prevEnd   = start.AddDays(-1);
            var prevStart = prevEnd.AddDays(-(length - 1));
            // Busy days are counted only up to today — the rest of the month hasn't happened yet
            var usedEnd  = end < today ? end : today;
            var usedDays = usedEnd >= start ? usedEnd.DayNumber - start.DayNumber + 1 : 0;

            // Real cars only — "RENTAL-…" are placeholders for partners' rent-a-cars
            var vehicles = await OrgVehicles.AsNoTracking()
                .Where(v => v.VehicleNumber == null || !v.VehicleNumber.StartsWith(AssignmentRules.RentalPlaceholderPrefix))
                .Where(v => vehicleId == null || v.VehicleId == vehicleId)
                .Select(v => new { v.VehicleId, v.VehicleName, v.VehicleNumber, v.VehicleType, v.Seatingcapacity })
                .OrderBy(v => v.VehicleName)
                .ToListAsync();
            var ids = vehicles.Select(v => v.VehicleId).ToList();

            // Trend: six months ending with the period's last month
            var trendStart = new DateOnly(end.Year, end.Month, 1).AddMonths(-5);
            var loadFrom   = new[] { prevStart, trendStart, today }.Min();
            var loadTo     = new[] { end, today.AddDays(60) }.Max();

            var trips = await OrgBookings.AsNoTracking()
                .Where(b => b.VehicleId != null && ids.Contains(b.VehicleId.Value)
                         && b.Status != Status.Canceled && b.ExternalEmployeeId == null
                         && b.travelDate <= loadTo
                         && (b.ReturnDate ?? b.travelDate) >= loadFrom)
                .Select(b => new
                {
                    b.BookingId, VehicleId = b.VehicleId!.Value, b.travelDate, b.ReturnDate, b.Traveltime,
                    b.Amount, b.BookingType, b.From, b.To,
                    CustomerName = b.Customer != null ? b.Customer.CustomerName : null,
                })
                .ToListAsync();

            // A self-drive rental keeps the car busy until it comes back
            DateOnly LastDay(DateOnly travel, DateOnly? ret, BookingType type) =>
                type == BookingType.SelfDriveRental && ret.HasValue && ret.Value > travel ? ret.Value : travel;

            var expFrom = (prevStart < trendStart ? prevStart : trendStart).ToDateTime(TimeOnly.MinValue);
            var expTo   = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var expenses = await OrgExpenses.AsNoTracking()
                .Where(e => e.ExpenseDate >= expFrom && e.ExpenseDate < expTo && e.CategoryType != Category.Salary
                         && (vehicleId == null || e.VehicleID == vehicleId))
                .Select(e => new { e.VehicleID, Day = DateOnly.FromDateTime(e.ExpenseDate), e.Amount, e.CategoryType })
                .ToListAsync();

            bool In(DateOnly d, DateOnly a, DateOnly b) => d >= a && d <= b;
            var curTrips  = trips.Where(t => In(t.travelDate, start, end)).ToList();
            var prevTrips = trips.Where(t => In(t.travelDate, prevStart, prevEnd)).ToList();
            var curExp    = expenses.Where(e => In(e.Day, start, end)).ToList();
            var prevExp   = expenses.Where(e => In(e.Day, prevStart, prevEnd)).ToList();

            // Days each car was out (distinct dates, rentals spread over their days)
            var busy = ids.ToDictionary(id => id, _ => new HashSet<DateOnly>());
            if (usedDays > 0)
                foreach (var t in trips)
                {
                    var first = t.travelDate < start ? start : t.travelDate;
                    var last  = LastDay(t.travelDate, t.ReturnDate, t.BookingType);
                    if (last > usedEnd) last = usedEnd;
                    for (var d = first; d <= last; d = d.AddDays(1)) busy[t.VehicleId].Add(d);
                }

            bool OutToday(int id) => trips.Any(t => t.VehicleId == id
                && t.travelDate <= today && LastDay(t.travelDate, t.ReturnDate, t.BookingType) >= today);

            // Next service due: the latest record of each service type decides
            var services = await _db.vehicleMaintenanceShedules.AsNoTracking()
                .Where(m => ids.Contains(m.VehicleID))
                .Select(m => new { m.VehicleID, m.maintenanceType, m.ServieDate, m.Nextduedate })
                .ToListAsync();
            var nextServices = services
                .GroupBy(m => new { m.VehicleID, m.maintenanceType })
                .Select(g => g.OrderByDescending(m => m.ServieDate).First())
                .Select(m => new { m.VehicleID, Type = m.maintenanceType.ToString(), Due = DateOnly.FromDateTime(m.Nextduedate) })
                .ToList();

            var docs = await _db.Documents.AsNoTracking()
                .Where(d => (!_tenant.ShouldFilter || d.OrgId == _tenant.OrgId)
                         && d.HasExpiry && d.ExpiryDate != null
                         && (vehicleId == null || d.VehicleID == vehicleId))
                .Select(d => new { d.DocumentID, d.Title, d.DocumentType, d.VehicleID, d.DriverName, d.ExpiryDate })
                .ToListAsync();
            var docAlerts = docs
                .Select(d => new { d, Expiry = DateOnly.FromDateTime(d.ExpiryDate!.Value) })
                .Where(x => x.Expiry <= today.AddDays(30))
                .OrderBy(x => x.Expiry)
                .ToList();

            var names = vehicles.ToDictionary(v => v.VehicleId, v => v);
            string? VName(int? id) => id.HasValue && names.TryGetValue(id.Value, out var v) ? v.VehicleName : null;
            string? VNum(int? id) => id.HasValue && names.TryGetValue(id.Value, out var v) ? v.VehicleNumber : null;

            var perVehicle = vehicles.Select(v =>
            {
                var vt = curTrips.Where(t => t.VehicleId == v.VehicleId).ToList();
                var ve = curExp.Where(e => e.VehicleID == v.VehicleId).ToList();
                var revenue = vt.Sum(t => t.Amount);
                var spent   = ve.Sum(e => e.Amount);
                var next = trips
                    .Where(t => t.VehicleId == v.VehicleId && t.travelDate >= today)
                    .OrderBy(t => t.travelDate).ThenBy(t => t.Traveltime)
                    .FirstOrDefault();
                var service = nextServices.Where(s => s.VehicleID == v.VehicleId).OrderBy(s => s.Due).FirstOrDefault();
                return new
                {
                    v.VehicleId,
                    v.VehicleName,
                    v.VehicleNumber,
                    VehicleType = v.VehicleType.ToString(),
                    Seats       = v.Seatingcapacity,
                    Trips       = vt.Count,
                    Revenue     = revenue,
                    Expenses    = spent,
                    Profit      = revenue - spent,
                    Fuel        = ve.Where(e => e.CategoryType == Category.Fuel).Sum(e => e.Amount),
                    Emi         = ve.Where(e => e.CategoryType == Category.EMI).Sum(e => e.Amount),
                    Repairs     = ve.Where(e => e.CategoryType is Category.Repair or Category.Service or Category.Tyre or Category.Towing).Sum(e => e.Amount),
                    BusyDays    = busy[v.VehicleId].Count,
                    Utilisation = usedDays == 0 ? 0 : Math.Round(100.0 * busy[v.VehicleId].Count / usedDays),
                    PerTrip     = vt.Count == 0 ? 0 : Math.Round(revenue / vt.Count),
                    OutToday    = OutToday(v.VehicleId),
                    NextTrip    = next == null ? null : new { next.BookingId, next.travelDate, next.Traveltime, next.From, next.To, next.CustomerName },
                    ServiceDue  = service == null ? null : new { service.Type, service.Due, DaysLeft = service.Due.DayNumber - today.DayNumber },
                    DocsDue     = docAlerts.Count(x => x.d.VehicleID == v.VehicleId),
                };
            }).OrderByDescending(v => v.Profit).ToList();

            decimal Pct(decimal now, decimal before) => before == 0 ? 0 : Math.Round(100 * (now - before) / Math.Abs(before));
            var revenueNow   = curTrips.Sum(t => t.Amount);
            var expensesNow  = curExp.Sum(e => e.Amount);
            var revenuePrev  = prevTrips.Sum(t => t.Amount);
            var expensesPrev = prevExp.Sum(e => e.Amount);
            var busyTotal    = perVehicle.Sum(v => v.BusyDays);

            var trend = Enumerable.Range(0, 6).Select(i =>
            {
                var m0 = trendStart.AddMonths(i);
                var m1 = m0.AddMonths(1).AddDays(-1);
                var rev = trips.Where(t => In(t.travelDate, m0, m1)).Sum(t => t.Amount);
                var exp = expenses.Where(e => In(e.Day, m0, m1)).Sum(e => e.Amount);
                return new { Label = m0.ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture), Year = m0.Year, Month = m0.Month, Revenue = rev, Expenses = exp, Profit = rev - exp };
            }).ToList();

            return ApiOk(new
            {
                From = start, To = end, Days = length, DaysSoFar = usedDays,
                Totals = new
                {
                    Vehicles    = vehicles.Count,
                    Trips       = curTrips.Count,
                    Revenue     = revenueNow,
                    Expenses    = expensesNow,
                    Profit      = revenueNow - expensesNow,
                    Margin      = revenueNow == 0 ? 0 : Math.Round(100 * (revenueNow - expensesNow) / revenueNow),
                    PerTrip     = curTrips.Count == 0 ? 0 : Math.Round(revenueNow / curTrips.Count),
                    Utilisation = vehicles.Count == 0 || usedDays == 0 ? 0 : Math.Round(100.0 * busyTotal / (vehicles.Count * usedDays)),
                    // Spent on no particular car (e.g. office fuel) — counted in Expenses, shown separately
                    Unlinked    = curExp.Where(e => e.VehicleID == null || !names.ContainsKey(e.VehicleID.Value)).Sum(e => e.Amount),
                    Previous    = new { Trips = prevTrips.Count, Revenue = revenuePrev, Expenses = expensesPrev, Profit = revenuePrev - expensesPrev },
                    Change      = new
                    {
                        Revenue  = Pct(revenueNow, revenuePrev),
                        Expenses = Pct(expensesNow, expensesPrev),
                        Profit   = Pct(revenueNow - expensesNow, revenuePrev - expensesPrev),
                        Trips    = Pct(curTrips.Count, prevTrips.Count),
                    },
                },
                Today = new { Out = perVehicle.Count(v => v.OutToday), Free = perVehicle.Count(v => !v.OutToday) },
                Vehicles = perVehicle,
                ByCategory = curExp.GroupBy(e => e.CategoryType.ToString())
                    .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
                    .OrderByDescending(g => g.Amount).ToList(),
                Trend = trend,
                Alerts = new
                {
                    Documents = docAlerts.Take(20).Select(x => new
                    {
                        x.d.DocumentID,
                        Title = x.d.Title ?? x.d.DocumentType ?? "Document",
                        x.d.VehicleID,
                        VehicleName   = VName(x.d.VehicleID),
                        VehicleNumber = VNum(x.d.VehicleID),
                        x.d.DriverName,
                        ExpiryDate = x.Expiry,
                        DaysLeft   = x.Expiry.DayNumber - today.DayNumber,
                    }),
                    Services = nextServices.Where(s => s.Due <= today.AddDays(15)).OrderBy(s => s.Due).Take(20).Select(s => new
                    {
                        s.VehicleID,
                        VehicleName   = VName(s.VehicleID),
                        VehicleNumber = VNum(s.VehicleID),
                        s.Type,
                        s.Due,
                        DaysLeft = s.Due.DayNumber - today.DayNumber,
                    }),
                },
            });
        }
    }
}
