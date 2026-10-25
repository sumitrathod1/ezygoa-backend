using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    /// <summary>Trips: a customer's visit grouping its package bookings and the shared hotel.</summary>
    public class TripRepository : ITripRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;
        private readonly IDispatchRepository _dispatch;

        public TripRepository(AppDbContext context, TenantContext tenant, IDispatchRepository dispatch)
        {
            _context  = context;
            _tenant   = tenant;
            _dispatch = dispatch;
        }

        private async Task<Trip> FindAsync(int tripId)
        {
            var trip = await _context.Trips.FirstOrDefaultAsync(t => t.TripId == tripId);
            if (trip == null || !_tenant.CanAccess(trip.OrgId))
                throw new KeyNotFoundException($"Trip {tripId} not found");
            return trip;
        }

        public async Task<object> FindByCustomerPhoneAsync(string phone)
        {
            var variants = BookingRepository.PhoneVariants(phone);
            if (BookingRepository.DigitsOf(phone).Length < 10) return new { customer = (object?)null, trips = Array.Empty<object>() };

            var orgId = _tenant.WriteOrgId;
            var customer = await _context.Customers.AsNoTracking()
                .Where(c => c.OrgId == orgId && c.CustomerNumber != null && variants.Contains(c.CustomerNumber))
                .OrderBy(c => c.CustomersId)
                .FirstOrDefaultAsync();
            if (customer == null) return new { customer = (object?)null, trips = Array.Empty<object>() };

            // Trips still running or only just finished — the ones a new booking would belong to
            var since = AssignmentRules.TodayInIndia().AddDays(-3);
            var trips = await _context.Trips.AsNoTracking()
                .Where(t => t.OrgId == orgId && t.CustomerId == customer.CustomersId && t.EndDate >= since)
                .OrderBy(t => t.StartDate)
                .Select(t => new
                {
                    t.TripId, t.StartDate, t.EndDate, t.StayName, t.StayLat, t.StayLng, t.StayMapsLink, t.Pax,
                    Bookings = t.Bookings.Count(b => b.Status != Status.Canceled),
                })
                .Take(5)
                .ToListAsync();

            // Bookings of this customer still to come that are not in any trip yet
            var today = AssignmentRules.TodayInIndia();
            var looseUpcoming = await _context.Bookings.AsNoTracking()
                .CountAsync(b => b.OrgId == orgId && b.CustomerID == customer.CustomersId && b.TripId == null
                              && b.Status != Status.Canceled && b.travelDate >= today);

            return new
            {
                customer = new { customer.CustomersId, customer.CustomerName, customer.CustomerNumber },
                trips,
                looseUpcoming,
            };
        }

        public async Task<object> GetAsync(int tripId)
        {
            var trip = await FindAsync(tripId);
            var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomersId == trip.CustomerId);

            var bookings = await _context.Bookings.AsNoTracking()
                .Where(b => b.TripId == tripId)
                .OrderBy(b => b.travelDate).ThenBy(b => b.Traveltime)
                .Select(b => new
                {
                    b.BookingId, TravelDate = b.travelDate, TravelTime = b.Traveltime, b.ReturnDate,
                    b.From, b.To, b.EndDrop, b.StartPickup, b.FullDayHours, b.Pax, b.Amount, BookingType = b.BookingType.ToString(), Status = b.Status.ToString(),
                    b.VehicleId, VehicleName = b.Vehicle != null ? b.Vehicle.VehicleName : null,
                    UserId = b.Userid, DriverName = b.user != null ? b.user.EmployeeName : null,
                    b.ExternalEmployeeId,
                    PartnerName = b.ExternalEmployee != null ? b.ExternalEmployee.externalEmployeeName : null,
                    Advance = _context.BookingPaymentAllocations
                        .Where(a => a.BookingId == b.BookingId && a.PayerType == PayerType.Customer)
                        .Sum(a => (decimal?)a.PaidAmount) ?? 0,
                })
                .ToListAsync();

            var active = bookings.Where(b => b.Status != nameof(Status.Canceled)).ToList();
            var total = active.Sum(b => b.Amount);
            var advance = bookings.Sum(b => b.Advance);

            return new
            {
                trip.TripId, trip.StartDate, trip.EndDate, trip.Pax, trip.Notes,
                trip.StayName, trip.StayLat, trip.StayLng, trip.StayMapsLink,
                Customer = customer == null ? null : new { customer.CustomersId, customer.CustomerName, customer.CustomerNumber },
                Bookings = bookings,
                Total = total,
                Advance = advance,
                Balance = total - advance,
            };
        }

        public async Task<object> UpdateStayAsync(int tripId, TripStayDTO dto)
        {
            var trip = await FindAsync(tripId);
            var name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name.Trim();
            trip.StayName     = name;
            trip.StayLat      = name == null ? null : dto.Lat;
            trip.StayLng      = name == null ? null : dto.Lng;
            trip.StayMapsLink = name == null ? null : (string.IsNullOrWhiteSpace(dto.MapsLink) ? null : dto.MapsLink.Trim());
            await _context.SaveChangesAsync();
            return new { trip.TripId, trip.StayName, trip.StayLat, trip.StayLng, trip.StayMapsLink };
        }

        public async Task<object> AssignAllAsync(int tripId, AssignBookingDTO dto)
        {
            await FindAsync(tripId);
            if (!(dto.VehicleId > 0) && !(dto.UserId > 0))
                throw new ArgumentException("Choose a driver or a vehicle.");

            var today = AssignmentRules.TodayInIndia();
            // Open own-fleet days from today on (past days keep whoever did them)
            var ids = await _context.Bookings.AsNoTracking()
                .Where(b => b.TripId == tripId && b.ExternalEmployeeId == null
                         && b.Status != Status.Canceled && b.Status != Status.Completed && b.travelDate >= today)
                .OrderBy(b => b.travelDate).Select(b => b.BookingId).ToListAsync();

            var results = new List<AssignBookingResult>();
            var skipped = new List<string>();
            foreach (var id in ids)
            {
                var change = new AssignBookingDTO { VehicleId = dto.VehicleId, UserId = dto.UserId };
                try { results.Add(await _dispatch.AssignAsync(id, change)); }
                catch (ArgumentException ex) { skipped.Add($"#{id}: {ex.Message}"); }   // e.g. rental-only car on a chauffeur day
            }

            return new
            {
                Updated  = results.Count,
                Results  = results,
                Warnings = results.SelectMany(r => r.Warnings).Distinct().ToList(),
                Skipped  = skipped,
            };
        }
    }
}
