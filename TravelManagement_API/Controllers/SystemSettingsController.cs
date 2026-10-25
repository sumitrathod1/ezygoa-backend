using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// App-wide settings for the super admin. Today: how long a sign-in lasts before
    /// everyone has to log in again (applies to new sign-ins).
    /// </summary>
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    [EnableRateLimiting("api")]
    public class SystemSettingsController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public SystemSettingsController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var s = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1) ?? new SystemSettings();
            return ApiOk(new { s.SessionDays, MaxSessionDays = SystemSettings.MaxSessionDays });
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] SystemSettingsDTO dto)
        {
            if (dto is null || dto.SessionDays < 1 || dto.SessionDays > SystemSettings.MaxSessionDays)
                return ApiBadRequest($"Sign-in length must be between 1 and {SystemSettings.MaxSessionDays} days.");

            var s = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Id == 1);
            if (s == null)
            {
                s = new SystemSettings { Id = 1 };
                _db.SystemSettings.Add(s);
            }
            s.SessionDays = dto.SessionDays;
            await _db.SaveChangesAsync();
            return ApiOk(new { s.SessionDays, MaxSessionDays = SystemSettings.MaxSessionDays },
                $"Sign-ins now last {s.SessionDays} day{(s.SessionDays == 1 ? "" : "s")} (from the next login).");
        }
    }

    public class SystemSettingsDTO
    {
        public int SessionDays { get; set; }
    }
}
