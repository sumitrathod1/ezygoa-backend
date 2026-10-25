using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using TravelManagement.API.Hubs;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.API.Controllers
{
    /// <summary>Trips: multi-day packages of one customer and their shared hotel.</summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class TripController : ApiControllerBase
    {
        private readonly ITripRepository _trips;
        private readonly IHubContext<BookingHub> _hub;
        private readonly TenantContext _tenant;

        public TripController(ITripRepository trips, IHubContext<BookingHub> hub, TenantContext tenant)
        {
            _trips  = trips;
            _hub    = hub;
            _tenant = tenant;
        }

        /// <summary>Is this phone number a returning customer, and are they on a trip right now?</summary>
        [HttpGet("by-customer")]
        public async Task<IActionResult> ByCustomer([FromQuery] string phone) =>
            ApiOk(await _trips.FindByCustomerPhoneAsync(phone ?? ""));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id) => ApiOk(await _trips.GetAsync(id));

        [HttpPut("{id:int}/stay")]
        public async Task<IActionResult> UpdateStay(int id, [FromBody] TripStayDTO dto)
        {
            if (dto is null) return ApiBadRequest("Hotel details are required");
            return ApiOk(await _trips.UpdateStayAsync(id, dto));
        }

        [HttpPost("{id:int}/assign-all")]
        public async Task<IActionResult> AssignAll(int id, [FromBody] AssignBookingDTO dto)
        {
            if (dto is null) return ApiBadRequest("Choose a driver or a vehicle.");
            var result = await _trips.AssignAllAsync(id, dto);

            await _hub.Clients.Group(HubGroups.OrgRole(_tenant.WriteOrgId, "Admin"))
                .SendAsync("ReceiveBookingUpdate", new { type = "ADMIN_BOOKING_REFRESH" });
            await _hub.Clients.Group(HubGroups.OrgRole(_tenant.WriteOrgId, "Employee"))
                .SendAsync("ReceiveBookingUpdate", new { type = "EMPLOYEE_BOOKING_REFRESH", driverId = dto.UserId });
            return ApiOk(result);
        }
    }
}
