using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// SuperAdmin-only endpoint for managing organizations and bootstrapping their first admin user.
    /// </summary>
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    [EnableRateLimiting("api")]
    public class OrganizationController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public OrganizationController(AppDbContext db) => _db = db;

        // ─────────────────────────────────────────────
        // GET  api/organization
        // ─────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orgs = await _db.Organizations
                .AsNoTracking()
                .OrderBy(o => o.OrgId)
                .ToListAsync();

            return ApiOk(orgs);
        }

        // ─────────────────────────────────────────────
        // GET  api/organization/{id}
        // ─────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var org = await _db.Organizations.FindAsync(id);
            return org is null ? ApiNotFound($"Organization {id} not found") : ApiOk(org);
        }

        // ─────────────────────────────────────────────
        // POST api/organization
        // Body: { name, code, email?, phone?, address?, logoUrl? }
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrgDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return ApiBadRequest("Organization name is required.");

            // Code uniqueness check
            if (!string.IsNullOrWhiteSpace(dto.Code) &&
                await _db.Organizations.AnyAsync(o => o.Code == dto.Code.ToUpperInvariant()))
                return ApiBadRequest($"Organization code '{dto.Code}' is already in use.");

            var org = new Organization
            {
                Name      = dto.Name.Trim(),
                Code      = string.IsNullOrWhiteSpace(dto.Code) ? "" : dto.Code.ToUpperInvariant().Trim(),
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
                Email     = dto.Email,
                Phone     = dto.Phone,
                Address   = dto.Address,
                LogoUrl   = dto.LogoUrl,
            };

            _db.Organizations.Add(org);
            await _db.SaveChangesAsync();

            return ApiCreated(org, $"Organization '{org.Name}' created (OrgId={org.OrgId}).");
        }

        // ─────────────────────────────────────────────
        // PUT  api/organization/{id}
        // ─────────────────────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateOrgDTO dto)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org is null) return ApiNotFound($"Organization {id} not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))    org.Name    = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Code))    org.Code    = dto.Code.ToUpperInvariant().Trim();
            if (dto.Email   != null) org.Email   = dto.Email;
            if (dto.Phone   != null) org.Phone   = dto.Phone;
            if (dto.Address != null) org.Address = dto.Address;
            if (dto.LogoUrl != null) org.LogoUrl = dto.LogoUrl;
            if (dto.IsActive.HasValue) org.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return ApiOk(org);
        }

        // ─────────────────────────────────────────────
        // POST api/organization/{id}/admin
        // Creates the first Admin user for this org.
        // Body: { employeeName, userName, password, number }
        // ─────────────────────────────────────────────
        [HttpPost("{id:int}/admin")]
        public async Task<IActionResult> CreateOrgAdmin(int id, [FromBody] CreateOrgAdminDTO dto)
        {
            var org = await _db.Organizations.FindAsync(id);
            if (org is null) return ApiNotFound($"Organization {id} not found.");

            if (string.IsNullOrWhiteSpace(dto.UserName) || string.IsNullOrWhiteSpace(dto.Password))
                return ApiBadRequest("UserName and Password are required.");

            // Unique username within the org
            bool taken = await _db.Users.AnyAsync(u => u.UserName == dto.UserName && u.OrgId == id);
            if (taken)
                return ApiBadRequest($"Username '{dto.UserName}' is already taken in this organization.");

            var admin = new User
            {
                EmployeeName = dto.EmployeeName ?? dto.UserName,
                UserName     = dto.UserName,
                Password     = PasswordHasher.HashPassword(dto.Password),
                Role         = Role.Admin,
                OrgId        = id,
                Status       = true,
                Number       = dto.Number ?? "0000000000",
            };

            _db.Users.Add(admin);
            await _db.SaveChangesAsync();

            return ApiCreated(new
            {
                admin.userId,
                admin.EmployeeName,
                admin.UserName,
                admin.Role,
                admin.OrgId,
                OrgName = org.Name,
            }, $"Admin '{admin.UserName}' created for organization '{org.Name}'.");
        }

        // ─────────────────────────────────────────────
        // GET  api/organization/{id}/users
        // ─────────────────────────────────────────────
        [HttpGet("{id:int}/users")]
        public async Task<IActionResult> GetOrgUsers(int id)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => u.OrgId == id && !u.IsDeleted)
                .Select(u => new { u.userId, u.EmployeeName, u.UserName, u.Role, u.Status, u.Number })
                .ToListAsync();

            return ApiOk(users);
        }

        // ─────────────────────────────────────────────
        // Linked organisations: admins can transfer bookings between linked orgs only
        // GET    api/organization/links
        // POST   api/organization/links        Body: { orgAId, orgBId }
        // DELETE api/organization/links/{id}
        // ─────────────────────────────────────────────
        [HttpGet("links")]
        public async Task<IActionResult> GetLinks()
        {
            var names = await _db.Organizations.AsNoTracking().ToDictionaryAsync(o => o.OrgId, o => o.Name);
            var links = await _db.OrgLinks.AsNoTracking().OrderBy(l => l.OrgLinkId).ToListAsync();
            return ApiOk(links.Select(l => new
            {
                l.OrgLinkId, l.OrgAId, l.OrgBId, l.CreatedAt,
                OrgAName = names.GetValueOrDefault(l.OrgAId),
                OrgBName = names.GetValueOrDefault(l.OrgBId),
            }));
        }

        [HttpPost("links")]
        public async Task<IActionResult> AddLink([FromBody] TravelManagement.Core.DTOs.OrgLinkDTO dto)
        {
            if (dto is null || dto.OrgAId <= 0 || dto.OrgBId <= 0)
                return ApiBadRequest("Choose two organisations.");
            if (dto.OrgAId == dto.OrgBId)
                return ApiBadRequest("Choose two different organisations.");

            var a = Math.Min(dto.OrgAId, dto.OrgBId);
            var b = Math.Max(dto.OrgAId, dto.OrgBId);
            var found = await _db.Organizations.CountAsync(o => o.OrgId == a || o.OrgId == b);
            if (found != 2) return ApiNotFound("Organisation not found.");
            if (await _db.OrgLinks.AnyAsync(l => l.OrgAId == a && l.OrgBId == b))
                return ApiBadRequest("These organisations are already linked.");

            var tenant = HttpContext.RequestServices.GetRequiredService<TenantContext>();
            var link = new OrgLink { OrgAId = a, OrgBId = b, CreatedByUserId = tenant.UserId > 0 ? tenant.UserId : null };
            _db.OrgLinks.Add(link);
            await _db.SaveChangesAsync();
            return ApiCreated(link, "Organisations linked. Their admins can now transfer bookings to each other.");
        }

        [HttpDelete("links/{id:int}")]
        public async Task<IActionResult> RemoveLink(int id)
        {
            var link = await _db.OrgLinks.FindAsync(id);
            if (link is null) return ApiNotFound("Link not found.");
            _db.OrgLinks.Remove(link);
            await _db.SaveChangesAsync();
            return ApiOk("Link removed. Bookings already transferred stay where they are.");
        }
    }

    // ─── DTOs (inline — no separate file needed) ────
    public class CreateOrgDTO
    {
        public string  Name     { get; set; } = string.Empty;
        public string? Code     { get; set; }
        public string? Email    { get; set; }
        public string? Phone    { get; set; }
        public string? Address  { get; set; }
        public string? LogoUrl  { get; set; }
        public bool?   IsActive { get; set; }
    }

    public class CreateOrgAdminDTO
    {
        public string? EmployeeName { get; set; }
        public string  UserName     { get; set; } = string.Empty;
        public string  Password     { get; set; } = string.Empty;
        public string? Number       { get; set; }
    }
}
