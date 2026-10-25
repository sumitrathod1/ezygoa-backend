using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelManagement.API.Hubs;
using TravelManagement.API.Reports;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class BookingController : ApiControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IHubContext<BookingHub> _hubContext;
        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public BookingController(
            IBookingService bookingService,
            IHubContext<BookingHub> hubContext,
            AppDbContext db,
            TenantContext tenant)
        {
            _bookingService = bookingService;
            _hubContext     = hubContext;
            _db             = db;
            _tenant         = tenant;
        }

        /// <summary>
        /// Returns full booking details for a single date — used by lazy-load popup.
        /// Only fetches data for one day, not the entire month.
        /// </summary>
        [HttpGet("by-date/{date}")]
        public async Task<IActionResult> GetBookingsByDate(DateOnly date)
        {
            var bookingQuery = _db.Bookings.AsNoTracking()
                .Include(b => b.user)
                .Include(b => b.CreatedBy)
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Include(b => b.ExternalEmployee)
                .Include(b => b.Trip)
                .Where(b => b.travelDate == date && b.Status != Status.Canceled
                         && b.ExternalEmployeeId == null);   // partner bookings are shown in "Extra", not the calendar

            if (_tenant.ShouldFilter)
                bookingQuery = bookingQuery.Where(b => b.OrgId == _tenant.OrgId);

            var bookings = await bookingQuery.OrderBy(b => b.Traveltime).ToListAsync();

            if (!bookings.Any())
                return ApiOk(Array.Empty<object>());

            var bookingIds = bookings.Select(b => b.BookingId).ToList();

            var payments = await _db.BookingPaymentAllocations
                .Where(p => bookingIds.Contains(p.BookingId))
                .GroupBy(p => p.BookingId)
                .Select(g => new
                {
                    BookingId = g.Key,
                    TotalPaid = g.Where(x => x.PayerType == PayerType.Customer)
                                 .Sum(x => x.PaidAmount),
                    CustomerWillPay = g.Where(x => x.PayerType == PayerType.Customer).Sum(x => x.AllocatedAmount),
                    OwnerWillPay    = g.Where(x => x.PayerType != PayerType.Customer).Sum(x => x.AllocatedAmount),
                })
                .ToListAsync();

            var payDict  = payments.ToDictionary(p => p.BookingId, p => p.TotalPaid);
            var allocDict = payments.ToDictionary(p => p.BookingId, p => p);

            // Commission is needed to edit a partner booking without losing it
            var commissions = await _db.ExternalEmployeeCashCollections
                .Where(s => bookingIds.Contains(s.BookingId))
                .GroupBy(s => s.BookingId)
                .Select(g => new { g.Key, Commission = g.Max(x => x.CommissionAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Commission);

            var result = bookings.Select(b => new
            {
                b.BookingId, b.travelDate, b.From, b.To,
                b.VehicleId, b.Vehicle,
                b.Userid,    b.user,
                b.BookingType, b.Traveltime, b.Status,
                b.Amount,    b.Pax, b.Assigned,
                b.CustomerID, b.Customer,
                b.ExternalEmployeeId, b.ExternalEmployee,
                b.Payment, b.TravelAgentId, b.TravelAgent,
                b.CreatedByUserId,
                CreatedByName = b.CreatedBy != null ? b.CreatedBy.EmployeeName : null,
                b.ReturnDate,
                b.TripId,
                b.EndDrop,
                b.StartPickup,
                b.FullDayHours,
                b.ReturnTime,
                b.SecurityDeposit,
                TripStayName     = b.Trip != null ? b.Trip.StayName : null,
                TripStayMapsLink = b.Trip != null ? b.Trip.StayMapsLink : null,
                AdvancePaid = payDict.GetValueOrDefault(b.BookingId, 0),
                Balance     = b.Amount - payDict.GetValueOrDefault(b.BookingId, 0),
                CustomerWillPay  = allocDict.TryGetValue(b.BookingId, out var a1) ? a1.CustomerWillPay : 0,
                OwnerWillPay     = allocDict.TryGetValue(b.BookingId, out var a2) ? a2.OwnerWillPay : 0,
                CommissionAmount = b.ExternalEmployeeId != null ? commissions.GetValueOrDefault(b.BookingId, 0) : (decimal?)null,
                EarnedAmount     = b.ExternalEmployeeId == null ? b.Amount : commissions.GetValueOrDefault(b.BookingId, 0),
            });

            return ApiOk(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingById(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);
            return booking is null
                ? ApiNotFound($"Booking with ID {id} not found")
                : ApiOk(booking);
        }

        [HttpGet("View-Bookings")]
        public async Task<IActionResult> ViewAllBookings()
        {
            var result = await _bookingService.GetAllBookingsWithStatsAsync();
            return result is null ? ApiNotFound("No bookings found") : ApiOk(result);
        }

        [HttpPost("New-Booking")]
        public async Task<IActionResult> NewBooking([FromBody] NewBookiingDTO bookingDTO)
        {
            if (bookingDTO is null)
                return ApiBadRequest("Booking data is required");

            var newBooking = await _bookingService.CreateBookingAsync(bookingDTO);

            // Only this organisation's screens refresh
            await _hubContext.Clients.Group(HubGroups.OrgRole(newBooking.OrgId, "Admin"))
                .SendAsync("ReceiveBookingUpdate", new { type = "ADMIN_BOOKING_REFRESH" });

            await _hubContext.Clients.Group(HubGroups.OrgRole(newBooking.OrgId, "Employee"))
                .SendAsync("ReceiveBookingUpdate", new
                {
                    type = "EMPLOYEE_BOOKING_REFRESH",
                    driverId = newBooking.Userid
                });

            return ApiCreated(newBooking, "Booking created successfully");
        }

        [HttpPut("cancel-booking")]
        public async Task<IActionResult> CancelBooking([FromBody] CancelBookingDTO dto)
        {
            if (dto is null) return ApiBadRequest("Cancellation data is required");
            bool result = await _bookingService.CancelBookingAsync(dto.BookingId);
            return result
                ? ApiOk("Booking cancelled successfully")
                : ApiNotFound("Booking not found");
        }

        [HttpGet("BookingFilter")]
        public async Task<IActionResult> GetFilteredBookings([FromQuery] BookingFilterDTO filterDTO)
        {
            var result = await _bookingService.FilterBookingsAsync(filterDTO);
            return ApiOk(result);
        }

        [HttpGet("vendors/bookings")]
        public async Task<IActionResult> GetVendorBookings([FromQuery] int? vendorId)
        {
            var result = await _bookingService.GetVendorBookingsAsync(vendorId);
            return ApiOk(result);
        }

        [HttpGet("externalEmployees")]
        public async Task<IActionResult> GetExternalEmployees()
        {
            var employees = await _bookingService.GetExternalEmployeesAsync();
            return ApiOk(employees);
        }

        [HttpPost("reassign-to-external")]
        public async Task<IActionResult> ReassignToExternal([FromBody] AssignExternalVendorDTO dto)
        {
            await _bookingService.ReassignToExternalAsync(dto);
            return ApiOk("Booking reassigned successfully");
        }

        [HttpPost("settle-settlement")]
        public async Task<IActionResult> SettleSettlement([FromBody] SettleSettlementDTO dto)
        {
            if (dto is null) return ApiBadRequest("Payment data is required");
            await _bookingService.SettleSettlementAsync(dto);
            return ApiOk("Payment recorded");
        }

        /// <summary>Itemised payment history of one partner booking.</summary>
        [HttpGet("settlements/{bookingId:int}/payments")]
        public async Task<IActionResult> GetSettlementHistory(int bookingId)
        {
            return ApiOk(await _bookingService.GetSettlementHistoryAsync(bookingId));
        }

        /// <summary>Undo a payment with a reversal entry (payments are never deleted).</summary>
        [HttpPost("settlement-payments/{id:int}/reverse")]
        public async Task<IActionResult> ReverseSettlementPayment(int id, [FromBody] ReverseSettlementPaymentDTO dto)
        {
            await _bookingService.ReverseSettlementPaymentAsync(id, dto?.Reason);
            return ApiOk("Payment reversed");
        }

        /// <summary>
        /// Partner commission statement as PDF. Period by travel date (from/to), or a single
        /// booking. Customer names are reduced to "First L." unless maskCustomer=false.
        /// </summary>
        [HttpGet("vendors/{vendorId:int}/statement")]
        public async Task<IActionResult> GetPartnerStatement(
            int vendorId,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            [FromQuery] int? bookingId = null,
            [FromQuery] bool maskCustomer = true,
            [FromQuery] bool showFare = true,
            [FromQuery] bool onlyOpen = false,
            [FromQuery] int? customerId = null)
        {
            if (from.HasValue && to.HasValue && from > to)
                return ApiBadRequest("'From' date must be before 'To' date");

            var data = await _bookingService.GetPartnerStatementAsync(vendorId, from, to, bookingId, maskCustomer, showFare, onlyOpen, customerId);
            var pdf  = PartnerStatementPdf.Generate(data);

            var safeName = new string(data.PartnerName.Where(char.IsLetterOrDigit).ToArray());
            var suffix = customerId.HasValue ? $"Customer{customerId}"
                : bookingId.HasValue ? $"Booking{bookingId}"
                : from.HasValue || to.HasValue ? $"{from:yyyyMMdd}-{to:yyyyMMdd}" : "All";
            return ApiFile(pdf, "application/pdf", $"Statement_{(safeName.Length > 0 ? safeName : "Partner")}_{suffix}.pdf");
        }
    }
}