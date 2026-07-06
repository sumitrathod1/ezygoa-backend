using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class PlacesController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public PlacesController(AppDbContext db) => _db = db;

        // GET api/places
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var query = _db.Places.AsNoTracking().Where(p => p.OrgId == 1);
            if (!includeInactive) query = query.Where(p => p.IsActive);

            var places = await query
                .OrderBy(p => p.Zone)
                .ThenBy(p => p.Name)
                .ToListAsync();

            return ApiOk(places);
        }

        // GET api/places/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var place = await _db.Places.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PlaceId == id && p.OrgId == 1);

            return place is null
                ? ApiNotFound($"Place {id} not found")
                : ApiOk(place);
        }

        // POST api/places
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] PlaceUpsertDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return ApiBadRequest("Place name is required");

            var place = new Place
            {
                Name               = dto.Name.Trim(),
                Aliases            = dto.Aliases?.Trim(),
                Zone               = dto.Zone,
                PickupAllowed      = dto.PickupAllowed,
                DropAllowed        = dto.DropAllowed,
                RestrictionMessage = dto.RestrictionMessage?.Trim(),
                IsActive           = dto.IsActive ?? true,
                OrgId              = 1,
            };

            _db.Places.Add(place);
            await _db.SaveChangesAsync();

            return ApiCreated(place, $"Place '{place.Name}' created");
        }

        // PUT api/places/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] PlaceUpsertDTO dto)
        {
            var place = await _db.Places
                .FirstOrDefaultAsync(p => p.PlaceId == id && p.OrgId == 1);

            if (place is null) return ApiNotFound($"Place {id} not found");

            if (!string.IsNullOrWhiteSpace(dto.Name))            place.Name               = dto.Name.Trim();
            if (dto.Aliases            != null)                   place.Aliases            = dto.Aliases.Trim();
            if (dto.RestrictionMessage != null)                   place.RestrictionMessage = dto.RestrictionMessage.Trim();
            if (dto.IsActive           != null)                   place.IsActive           = dto.IsActive.Value;
            place.Zone          = dto.Zone;
            place.PickupAllowed = dto.PickupAllowed;
            place.DropAllowed   = dto.DropAllowed;

            await _db.SaveChangesAsync();
            return ApiOk(place, "Place updated");
        }
    }

    public class PlaceUpsertDTO
    {
        public string      Name               { get; set; } = string.Empty;
        public string?     Aliases            { get; set; }
        public PlaceZone   Zone               { get; set; }
        public bool        PickupAllowed      { get; set; } = true;
        public bool        DropAllowed        { get; set; } = true;
        public string?     RestrictionMessage { get; set; }
        public bool?       IsActive           { get; set; }
    }
}
