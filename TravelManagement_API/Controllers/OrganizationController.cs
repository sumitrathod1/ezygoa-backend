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
