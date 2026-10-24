using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Models;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]   // reads were anonymous and returned every org's charts
    [EnableRateLimiting("api")]
    public class RateChartController : ApiControllerBase
    {
        private readonly IRateChartService _service;

        public RateChartController(IRateChartService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var charts = await _service.GetAllAsync();
            return ApiOk(charts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var chart = await _service.GetByIdAsync(id);
            return chart is null
                ? ApiNotFound($"Rate chart '{id}' not found")
                : ApiOk(chart);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RateChart chart)
        {
            if (chart is null) return ApiBadRequest("Rate chart data is required");
            var created = await _service.CreateAsync(chart);
            return ApiCreated(created, "Rate chart created successfully");
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] RateChart chart)
        {
            if (chart is null) return ApiBadRequest("Rate chart data is required");
            var updated = await _service.UpdateAsync(id, chart);
            return updated is null
                ? ApiNotFound($"Rate chart '{id}' not found")
                : ApiOk(updated, "Rate chart updated successfully");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var deleted = await _service.DeleteAsync(id);
            return deleted
                ? ApiOk<object>(null, "Rate chart deleted successfully")
                : ApiNotFound($"Rate chart '{id}' not found");
        }
    }
}
