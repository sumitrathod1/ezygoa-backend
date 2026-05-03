using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Models;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    [Authorize]
    public class SalaryController : ApiControllerBase
    {
        private readonly ISalaryService _service;

        public SalaryController(ISalaryService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return ApiOk(list);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var list = await _service.GetByUserAsync(userId);
            return ApiOk(list);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Salary salary)
        {
            if (salary is null) return ApiBadRequest("Salary data is required");
            var created = await _service.CreateAsync(salary);
            return ApiCreated(created, "Salary record created");
        }

        [HttpPost("generate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateMonth([FromQuery] int month, [FromQuery] int year)
        {
            var list = await _service.GenerateMonthAsync(month, year);
            return ApiOk(list, $"Generated {list.Count} salary records for {month}/{year}");
        }

        [HttpPut("{id}/pay")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkPaid(int id, [FromQuery] string? notes)
        {
            var updated = await _service.MarkPaidAsync(id, DateTime.UtcNow, notes);
            return updated is null
                ? ApiNotFound($"Salary record {id} not found")
                : ApiOk(updated, "Marked as paid");
        }
    }
}
