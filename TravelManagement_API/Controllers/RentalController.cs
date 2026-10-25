using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Implementation;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// Rent a Car: self-drive rentals with our own cars or a partner's car. Rentals are
    /// saved through the normal booking save (POST Booking/New-Booking, type SelfDriveRental);
    /// this controller lists them and marks a car as returned.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class RentalController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public RentalController(AppDbContext db, TenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        private IQueryable<Booking> OrgRentals =>
            (_tenant.ShouldFilter ? _db.Bookings.Where(b => b.OrgId == _tenant.OrgId) : _db.Bookings)
                .Where(b => b.BookingType == BookingType.SelfDriveRental);

        /// <summary>Rentals from 60 days ago onwards (or the given range), with their state.</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
        {
            var today = AssignmentRules.TodayInIndia();
            var start = from ?? today.AddDays(-60);
            var end   = to ?? today.AddDays(365);

            var rows = await OrgRentals.AsNoTracking()
                .Where(b => b.Status != Status.Canceled
                         && b.travelDate <= end && (b.ReturnDate ?? b.travelDate) >= start)
                .OrderBy(b => b.travelDate).ThenBy(b => b.Traveltime)
                .Select(b => new
                {
                    b.BookingId, b.travelDate, b.Traveltime, b.ReturnDate, b.ReturnTime,
                    b.From, b.To, b.Pax, b.Amount, b.Status, b.SecurityDeposit, b.TripId, b.TravelAgentId,
                    b.VehicleId,
                    VehicleName   = b.Vehicle != null ? b.Vehicle.VehicleName : null,
                    VehicleNumber = b.Vehicle != null ? b.Vehicle.VehicleNumber : null,
                    b.ExternalEmployeeId,
                    PartnerName   = b.ExternalEmployee != null ? b.ExternalEmployee.externalEmployeeName : null,
                    PartnerNumber = b.ExternalEmployee != null ? b.ExternalEmployee.externalEmployeeNumber : null,
                    CustomerName   = b.Customer != null ? b.Customer.CustomerName : null,
                    CustomerNumber = b.Customer != null ? b.Customer.CustomerNumber : null,
                })
                .ToListAsync();

            var ids = rows.Select(r => r.BookingId).ToList();
            var paid = await _db.BookingPaymentAllocations.AsNoTracking()
                .Where(a => ids.Contains(a.BookingId) && a.PayerType == PayerType.Customer)
                .GroupBy(a => a.BookingId)
                .Select(g => new { g.Key, Paid = g.Sum(x => x.PaidAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Paid);
            var settlements = await _db.ExternalEmployeeCashCollections.AsNoTracking()
                .Where(s => ids.Contains(s.BookingId))
                .Select(s => new { s.BookingId, s.CommissionAmount, s.CashCollectedBy })
                .ToDictionaryAsync(s => s.BookingId);

            return ApiOk(rows.Select(r =>
            {
                var returnDate = r.ReturnDate ?? r.travelDate;
                var state = r.Status == Status.Completed ? "returned"
                    : r.travelDate > today ? "upcoming"
                    : returnDate < today ? "overdue"
                    : "out";
                var p = paid.GetValueOrDefault(r.BookingId, 0);
                settlements.TryGetValue(r.BookingId, out var s);
                return new
                {
                    r.BookingId,
                    PickupDate = r.travelDate, PickupTime = r.Traveltime,
                    ReturnDate = returnDate, r.ReturnTime,
                    Days = Math.Max(1, returnDate.DayNumber - r.travelDate.DayNumber),
                    PickupLocation = r.From, ReturnLocation = r.To,
                    r.Pax, r.Amount, Paid = p, Balance = Math.Max(0, r.Amount - p), r.SecurityDeposit,
                    Status = r.Status.ToString(), State = state,
                    r.VehicleId, r.VehicleName, r.VehicleNumber,
                    r.ExternalEmployeeId, r.PartnerName, r.PartnerNumber,
                    Commission = s?.CommissionAmount,
                    CashWithPartner = s != null && s.CashCollectedBy == CashCollectedBy.ExternalEmployee,
                    r.CustomerName, r.CustomerNumber, r.TripId, r.TravelAgentId,
                };
            }));
        }

        /// <summary>The car is back: the rental is completed.</summary>
        [HttpPost("{id:int}/returned")]
        public async Task<IActionResult> Returned(int id)
        {
            var b = await _db.Bookings.FirstOrDefaultAsync(x => x.BookingId == id);
            if (b == null || !_tenant.CanAccess(b.OrgId) || b.BookingType != BookingType.SelfDriveRental)
                return ApiNotFound("Rental not found");
            if (b.Status == Status.Canceled) return ApiBadRequest("This rental is cancelled.");
            if (b.Status == Status.Completed) return ApiOk("Already returned");
            b.Status = Status.Completed;
            await _db.SaveChangesAsync();
            return ApiOk("Marked as returned");
        }
    }
}
