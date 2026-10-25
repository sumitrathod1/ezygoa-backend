using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    /// <summary>Builds customer invoices for one booking, its trip, or all of the customer's bookings.</summary>
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public InvoiceRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        public async Task<CustomerInvoiceData> GetAsync(int bookingId, string? scope, bool includeTerms = false)
        {
            var booking = await _context.Bookings.AsNoTracking()
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null || !_tenant.CanAccess(booking.OrgId))
                throw new KeyNotFoundException($"Booking {bookingId} not found");

            var orgBookings = _context.Bookings.AsNoTracking().Where(b => b.OrgId == booking.OrgId);
            var customerQ = orgBookings.Where(b => b.CustomerID == booking.CustomerID && b.Status != Status.Canceled);
            var tripQ = booking.TripId != null
                ? orgBookings.Where(b => b.TripId == booking.TripId && b.Status != Status.Canceled)
                : orgBookings.Where(b => false);
            var tripCount = await tripQ.CountAsync();
            var customerCount = await customerQ.CountAsync();

            scope = (scope ?? "booking").Trim().ToLowerInvariant();
            // auto: the whole trip, else all of the customer's bookings when there are several
            if (scope == "auto") scope = tripCount > 1 ? "trip" : customerCount > 1 ? "customer" : "booking";
            if (scope == "trip" && booking.TripId == null) scope = "booking";
            if (scope is not ("booking" or "trip" or "customer")) scope = "booking";

            var selected = scope switch
            {
                "trip"     => tripQ,
                "customer" => customerQ,
                _          => orgBookings.Where(b => b.BookingId == bookingId),
            };

            var rows = await selected
                .OrderBy(b => b.travelDate).ThenBy(b => b.Traveltime)
                .Select(b => new
                {
                    b.BookingId, b.travelDate, b.ReturnDate, b.Traveltime, b.BookingType, b.From, b.To, b.Pax,
                    b.Amount, b.Status, b.ExternalEmployeeId, b.EndDrop, b.StartPickup, b.FullDayHours,
                    RateCategory = b.Vehicle != null ? b.Vehicle.RateCategory : null,
                    Vehicle = b.Vehicle != null ? b.Vehicle.VehicleName : null,
                    Driver  = b.user != null ? b.user.EmployeeName : null,
                })
                .ToListAsync();

            var ids = rows.Select(r => r.BookingId).ToList();
            var paid = await _context.BookingPaymentAllocations.AsNoTracking()
                .Where(a => ids.Contains(a.BookingId) && a.PayerType == PayerType.Customer)
                .GroupBy(a => a.BookingId)
                .Select(g => new { g.Key, Paid = g.Sum(x => x.PaidAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Paid);

            var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrgId == booking.OrgId);
            var stay = booking.TripId != null
                ? await _context.Trips.AsNoTracking().Where(t => t.TripId == booking.TripId).Select(t => t.StayName).FirstOrDefaultAsync()
                : null;

            var today = AssignmentRules.TodayInIndia();
            var prefix = InvoicePrefix(org);
            var data = new CustomerInvoiceData
            {
                OrgName        = org?.Name ?? "",
                OrgPhone       = org?.Phone,
                OrgEmail       = org?.Email,
                OrgAddress     = org?.Address,
                OrgLogo        = org?.LogoUrl,
                Scope          = scope,
                InvoiceDate    = today,
                InvoiceNo      = scope switch
                {
                    "trip"     => $"{prefix}-{today.Year}-T{booking.TripId}",
                    "customer" => $"{prefix}-{today.Year}-C{booking.CustomerID}",
                    _          => $"{prefix}-{today.Year}-{booking.BookingId}",
                },
                CustomerName   = booking.Customer?.CustomerName ?? "",
                CustomerNumber = booking.Customer?.CustomerNumber,
                StayName       = stay,
                TripBookings     = tripCount,
                CustomerBookings = customerCount,
            };

            // A package's advance is stored on its first day; whatever is more than that day's
            // amount counts towards the next bookings on the invoice (else it vanished)
            decimal carry = 0;
            foreach (var r in rows)
            {
                var available = paid.GetValueOrDefault(r.BookingId, 0) + carry;
                var p = r.Status == Status.Canceled ? 0 : Math.Min(r.Amount, available);
                carry = available - p;
                var own = r.ExternalEmployeeId == null;
                data.Lines.Add(new CustomerInvoiceLine
                {
                    BookingId   = r.BookingId,
                    TravelDate  = r.travelDate,
                    ReturnDate  = r.ReturnDate,
                    TravelTime  = r.Traveltime,
                    BookingType = r.BookingType.ToString(),
                    TypeLabel   = BookingLabels.Label(r.BookingType, r.EndDrop, r.StartPickup, r.FullDayHours),
                    EndDrop     = r.EndDrop,
                    From = r.From, To = r.To, Pax = r.Pax,
                    Vehicle = own ? r.Vehicle : null,
                    Driver  = own ? r.Driver : null,
                    Amount  = r.Amount,
                    Paid    = p,
                    Balance = Math.Max(0, r.Amount - p),
                    Status  = r.Status.ToString(),
                });
            }
            var live = rows.Where(r => r.Status != Status.Canceled).ToList();
            data.HasFullDay = live.Any(r => r.BookingType == BookingType.FullDay);
            if (includeTerms)
            {
                var terms = await InvoiceTermsStore.GetAsync(_context, booking.OrgId);
                var block = new InvoiceTermsBlock();
                bool IsTransfer(BookingType t) => t is BookingType.AirportPickup or BookingType.AirportDrop or BookingType.RailwayStation;

                foreach (var n in terms.NightCharges.Where(n => n.Amount > 0))
                {
                    var applies = n.AppliesTo switch
                    {
                        "All"      => live.Count > 0,
                        // Pickup / drop only when its time falls inside the window (or the time is not known yet)
                        "Transfer" => live.Any(r => IsTransfer(r.BookingType)
                                        && (r.Traveltime == null || InvoiceTermsStore.InWindow(r.Traveltime.Value, n.From, n.To))),
                        _          => data.HasFullDay,
                    };
                    if (applies) block.NightCharges.Add(n);
                }

                var fullDays = live.Where(r => r.BookingType == BookingType.FullDay).ToList();
                if (fullDays.Count > 0)
                {
                    block.Packages = fullDays.Where(r => r.FullDayHours is 8 or 12)
                        .Select(r => $"{r.FullDayHours} hr / {r.FullDayHours * 10} km").Distinct().ToList();
                    // Only the booked car's row when every Full Day has an own car with a known category
                    var cats = fullDays.Select(r => r.ExternalEmployeeId == null ? r.RateCategory : null).ToList();
                    var matched = terms.ExtraRates.Where(x => cats.Any(c => c != null && string.Equals(c, x.Category, StringComparison.OrdinalIgnoreCase))).ToList();
                    block.ExtraRatesForVehicle = cats.All(c => c != null) && matched.Count > 0;
                    block.ExtraRates = block.ExtraRatesForVehicle ? matched : terms.ExtraRates;
                }

                block.OtherTerms = (terms.OtherTerms ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                data.Terms = block;
            }

            data.Total   = data.Lines.Sum(l => l.Amount);
            data.Paid    = data.Lines.Sum(l => l.Paid);
            data.Balance = data.Lines.Sum(l => l.Balance);
            return data;
        }

        /// <summary>"EZYGOA" from the org code, else the first letters of its name ("ZipGoa" → "ZIP").</summary>
        private static string InvoicePrefix(Organization? org)
        {
            if (!string.IsNullOrWhiteSpace(org?.Code)) return org!.Code.Trim().ToUpperInvariant();
            var letters = new string((org?.Name ?? "INV").Where(char.IsLetter).ToArray()).ToUpperInvariant();
            return letters.Length == 0 ? "INV" : letters[..Math.Min(3, letters.Length)];
        }
    }
}
