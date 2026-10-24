using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TravelManagement.API.Hubs;
using TravelManagement.Core.DTOs;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// Assigning drivers / vehicles after a booking is taken, the "still to assign"
    /// header badge, and one-click hand-over of a booking to a linked organisation.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/Booking")]
    [EnableRateLimiting("api")]
    public class BookingDispatchController : ApiControllerBase
    {
        private readonly IDispatchRepository _dispatch;
        private readonly IHubContext<BookingHub> _hub;
        private readonly INotificationService _notifications;
        private readonly ILogger<BookingDispatchController> _logger;

        public BookingDispatchController(
            IDispatchRepository dispatch,
            IHubContext<BookingHub> hub,
            INotificationService notifications,
            ILogger<BookingDispatchController> logger)
        {
            _dispatch      = dispatch;
            _hub           = hub;
            _notifications = notifications;
            _logger        = logger;
        }

        /// <summary>Bookings in the range with their driver / vehicle, plus all drivers and vehicles.</summary>
        [HttpGet("assignment-board")]
        public async Task<IActionResult> Board([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            var end   = to ?? start.AddDays(6);
            return ApiOk(await _dispatch.GetBoardAsync(start, end));
        }

        /// <summary>Upcoming bookings still missing a driver or vehicle (header badge).</summary>
        [HttpGet("pending-assignment-count")]
        public async Task<IActionResult> PendingCount() => ApiOk(await _dispatch.GetPendingAssignmentCountAsync());

        [HttpPut("{id:int}/assign")]
        public async Task<IActionResult> Assign(int id, [FromBody] AssignBookingDTO dto)
        {
            if (dto is null) return ApiBadRequest("Assignment data is required");
            var result = await _dispatch.AssignAsync(id, dto);
            await Refresh(result.OrgId, result.UserId);
            return ApiOk(result);
        }

        /// <summary>Organisations this one is linked with (set up by the super admin).</summary>
        [HttpGet("transfer-targets")]
        public async Task<IActionResult> TransferTargets() => ApiOk(await _dispatch.GetTransferTargetsAsync());

        [HttpPost("{id:int}/transfer")]
        public async Task<IActionResult> Transfer(int id, [FromBody] TransferBookingDTO dto)
        {
            if (dto is null || dto.TargetOrgId <= 0) return ApiBadRequest("Choose the organisation to transfer to.");
            var result = await _dispatch.TransferAsync(id, dto);

            await Refresh(result.FromOrgId, null);
            await Refresh(result.ToOrgId, null);

            // The receiving office must notice it — it still needs a driver and vehicle there
            try
            {
                var msg = $"{result.FromOrgName} sent you booking #{result.BookingId}. Please assign a driver and vehicle.";
                await _notifications.CreateAsync(msg, result.ToOrgId);
                await _notifications.SendPushNotificationAsync("New booking transferred to you", msg, result.ToOrgId);
            }
            catch (Exception ex)
            {
                // The transfer itself is saved; a failed push must not undo it
                _logger.LogWarning(ex, "Transfer notification failed for booking {BookingId}", result.BookingId);
            }

            return ApiOk(result);
        }

        private async Task Refresh(int orgId, int? driverId)
        {
            await _hub.Clients.Group(HubGroups.OrgRole(orgId, "Admin"))
                .SendAsync("ReceiveBookingUpdate", new { type = "ADMIN_BOOKING_REFRESH" });
            await _hub.Clients.Group(HubGroups.OrgRole(orgId, "Employee"))
                .SendAsync("ReceiveBookingUpdate", new { type = "EMPLOYEE_BOOKING_REFRESH", driverId });
        }
    }
}
