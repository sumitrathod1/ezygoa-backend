using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    /// <summary>
    /// The signed-in user's own organisation: name and logo for the app header, and the
    /// contact details (phone / email / address) printed on invoices. Admins can change the
    /// contact details and upload the logo; the name is managed by the super admin.
    /// </summary>
    [Authorize(Roles = "Admin,Employee")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class MyOrganizationController : ApiControllerBase
    {
        /// <summary>Logos are stored as small images inside the database (the app shrinks them first).</summary>
        private const int MaxLogoChars = 400_000;   // ≈ 300 KB image
        private static readonly Regex LogoData = new(@"^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$", RegexOptions.Compiled);

        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public MyOrganizationController(AppDbContext db, TenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrgId == _tenant.WriteOrgId);
            if (org == null) return ApiNotFound("Organisation not found");
            return ApiOk(new { org.OrgId, org.Name, org.Code, org.Phone, org.Email, org.Address, org.LogoUrl });
        }

        /// <summary>Contact details printed on invoices and statements.</summary>
        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update([FromBody] OrgContactDTO dto)
        {
            if (dto is null) return ApiBadRequest("Details are required");
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OrgId == _tenant.WriteOrgId);
            if (org == null) return ApiNotFound("Organisation not found");
            if (!string.IsNullOrWhiteSpace(dto.Phone) && dto.Phone.Count(char.IsDigit) < 10)
                return ApiBadRequest("Enter a valid phone number (10 digits).");
            if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Contains('@'))
                return ApiBadRequest("Enter a valid email address.");

            org.Phone   = Clean(dto.Phone);
            org.Email   = Clean(dto.Email);
            org.Address = Clean(dto.Address);
            await _db.SaveChangesAsync();
            return ApiOk(new { org.OrgId, org.Name, org.Code, org.Phone, org.Email, org.Address, org.LogoUrl }, "Details saved");
        }

        /// <summary>Logo as a data URL (PNG / JPEG / WebP). Empty = remove the logo.</summary>
        [HttpPut("logo")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetLogo([FromBody] OrgLogoDTO dto)
        {
            var org = await _db.Organizations.FirstOrDefaultAsync(o => o.OrgId == _tenant.WriteOrgId);
            if (org == null) return ApiNotFound("Organisation not found");

            var logo = dto?.Logo?.Trim();
            if (string.IsNullOrEmpty(logo))
            {
                org.LogoUrl = null;
            }
            else
            {
                if (logo.Length > MaxLogoChars) return ApiBadRequest("The logo is too large. Please use a smaller image.");
                if (!LogoData.IsMatch(logo)) return ApiBadRequest("Please upload a PNG, JPG or WebP image.");
                org.LogoUrl = logo;
            }
            await _db.SaveChangesAsync();
            return ApiOk(new { org.LogoUrl }, org.LogoUrl == null ? "Logo removed" : "Logo saved");
        }

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    public class OrgContactDTO
    {
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class OrgLogoDTO
    {
        public string? Logo { get; set; }
    }
}
