using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelManagement.Core.Models;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;

namespace TravelManagement.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class TravelAgentsController : ApiControllerBase
    {
        private readonly ITravelAgentsService _travelAgentsService;

        public TravelAgentsController(ITravelAgentsService travelAgentsService)
        {
            _travelAgentsService = travelAgentsService;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAgent(int id, [FromBody] AddAgentDTO dto)
        {
            if (dto is null) return ApiBadRequest("Agent data is required");
            var updated = await _travelAgentsService.UpdateAgentAsync(id, dto);
            return updated is null
                ? ApiNotFound($"Agent {id} not found")
                : ApiOk(updated, "Agent updated successfully");
        }

        [HttpPost("AddAgent")]
        public async Task<IActionResult> AddAgent([FromBody] AddAgentDTO dto)
        {
            if (dto is null) return ApiBadRequest("Travel agent data is required");
            var agent = await _travelAgentsService.AddAgentAsync(dto);
            return ApiCreated(agent, "Travel agent added successfully");
        }

        [HttpGet("GetAllAgent")]
        public async Task<IActionResult> GetAllAgent()
        {
            // An org with no agents yet is an empty list, not an error
            var agents = await _travelAgentsService.GetAllAgentsDashboardAsync();
            return ApiOk(agents ?? new List<AgentDashboardDTO>());
        }

        [HttpPost("ApplyAgentPayment")]
        public async Task<IActionResult> ApplyAgentPayment([FromBody] AddAgentPaymentDto dto)
        {
            if (dto is null || dto.TotalPaidAmount <= 0)
                return ApiBadRequest("Invalid payment data");

            decimal applied = await _travelAgentsService.ApplyAgentPaymentAsync(dto);
            if (applied <= 0)
                return ApiBadRequest("No pending amount to apply for this agent");

            var unapplied = dto.TotalPaidAmount - applied;
            return unapplied > 0
                ? ApiOk($"₹{applied:0.##} applied. ₹{unapplied:0.##} is more than this agent owes and was not recorded.")
                : ApiOk($"Payment of ₹{applied:0.##} applied successfully");
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> AgentBookingByID(int id)
        {
            var agent = await _travelAgentsService.GetAgentByIdAsync(id);
            if (agent is null) return ApiNotFound($"Travel agent with ID {id} not found");

            var bookings = await _travelAgentsService.GetAgentBookingsByIdAsync(id);
            return ApiOk(bookings ?? new List<Booking>());
        }

        [HttpGet("ExportAgentBookingsPdf/{agentId}")]
        public async Task<IActionResult> ExportAgentBookingsPdf(
            int agentId,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            var bookings = await _travelAgentsService.GetAgentReportBookingsByIdAsync(agentId, fromDate, toDate);
            if (bookings is null || bookings.Count == 0)
                return ApiNotFound("No bookings found for this agent");

            var agentName = bookings.First().TravelAgent?.Name ?? "Unknown Agent";
            var pdfBytes = BookingPdfGenerator.Generate(bookings, agentName, fromDate, toDate);

            string fileName = fromDate.HasValue && toDate.HasValue
                ? $"Agent_{agentId}_BookingsReport_{fromDate:ddMMyyyy}_{toDate:ddMMyyyy}.pdf"
                : $"Agent_{agentId}_BookingsReport_All.pdf";

            return ApiFile(pdfBytes, "application/pdf", fileName);
        }
    }
}