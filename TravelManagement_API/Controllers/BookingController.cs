using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TravelManagement.API.Hubs;
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
                .Where(b => b.travelDate == date && b.Status != Status.Canceled);

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
                                 .Sum(x => x.PaidAmount)
                })
                .ToListAsync();

            var payDict = payments.ToDictionary(p => p.BookingId, p => p.TotalPaid);

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
                AdvancePaid = payDict.GetValueOrDefault(b.BookingId, 0),
                Balance     = b.Amount - payDict.GetValueOrDefault(b.BookingId, 0)
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

            await _hubContext.Clients.Group("Admin")
                .SendAsync("ReceiveBookingUpdate", new { type = "ADMIN_BOOKING_REFRESH" });

            await _hubContext.Clients.Group("Employee")
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
            await _bookingService.SettleSettlementAsync(dto);
            return ApiOk("Settlement completed successfully");
        }
    }
}