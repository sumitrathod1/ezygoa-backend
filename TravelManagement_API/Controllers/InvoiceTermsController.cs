using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Implementation;

namespace TravelManagement.API.Controllers
{
    /// <summary>Invoice terms &amp; conditions of the caller's organisation (night charges, extra hour / km).</summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class InvoiceTermsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TenantContext _tenant;

        public InvoiceTermsController(AppDbContext db, TenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> Get() => ApiOk(await InvoiceTermsStore.GetAsync(_db, _tenant.WriteOrgId));

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] InvoiceTermsDTO dto)
        {
            if (dto is null) return ApiBadRequest("Terms are required");
            return ApiOk(await InvoiceTermsStore.SaveAsync(_db, _tenant.WriteOrgId, dto), "Invoice terms saved");
        }
    }
}
