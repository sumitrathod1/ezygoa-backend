using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    /// <summary>Read-only view of the audit trail (rows are written automatically on save).</summary>
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("api")]
    public class AuditLogController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public AuditLogController(AppDbContext db, TenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int? bookingId,
            [FromQuery] string? entityType,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page = Math.Max(1, page);

            var q = _db.AuditLogs.AsNoTracking().AsQueryable();
            if (_tenant.ShouldFilter) q = q.Where(a => a.OrgId == _tenant.OrgId);
            if (bookingId.HasValue) q = q.Where(a => a.BookingId == bookingId.Value);
            if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
            if (from.HasValue)
            {
                var f = from.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
                q = q.Where(a => a.CreatedAt >= f);
            }
            if (to.HasValue)
            {
                var t = to.Value.ToDateTime(TimeOnly.MaxValue).ToUniversalTime();
                q = q.Where(a => a.CreatedAt <= t);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                q = q.Where(a => a.Summary.ToLower().Contains(s) || (a.UserName != null && a.UserName.ToLower().Contains(s)));
            }

            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new
                {
                    a.AuditLogId, a.CreatedAt, a.UserName, a.Action, a.EntityType,
                    a.EntityId, a.BookingId, a.Summary, a.Changes,
                })
                .ToListAsync();

            return ApiOk(new { total, page, pageSize, rows });
        }
    }
}
