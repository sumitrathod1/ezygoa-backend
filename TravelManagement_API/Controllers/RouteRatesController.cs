using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class RouteRatesController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        private readonly TenantContext _tenant;

        public RouteRatesController(AppDbContext db, TenantContext tenant)
        {
            _db     = db;
            _tenant = tenant;
        }

        // Was hardcoded to OrgId 1, so every organisation read and wrote EzyGoa's data
        private int Org => _tenant.WriteOrgId;

        // GET api/routerates?fromPlaceId=&toPlaceId=&vehicleType=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int?    fromPlaceId  = null,
            [FromQuery] int?    toPlaceId    = null,
            [FromQuery] string? vehicleType  = null,
            [FromQuery] bool    includeInactive = false)
        {
            var query = _db.RouteRates
                .AsNoTracking()
                .Include(r => r.FromPlace)
                .Include(r => r.ToPlace)
                .Where(r => r.OrgId == Org);

            if (!includeInactive) query = query.Where(r => r.IsActive);
            if (fromPlaceId.HasValue) query = query.Where(r => r.FromPlaceId == fromPlaceId.Value);
            if (toPlaceId.HasValue)   query = query.Where(r => r.ToPlaceId   == toPlaceId.Value);
            if (!string.IsNullOrWhiteSpace(vehicleType))
                query = query.Where(r => r.VehicleType == vehicleType);

            var rates = await query
                .OrderBy(r => r.FromPlace!.Name)
                .ThenBy(r => r.ToPlace!.Name)
                .ThenBy(r => r.VehicleType)
                .ToListAsync();

            return ApiOk(rates);
        }

        // GET api/routerates/quote?fromPlaceId=1&toPlaceId=20
        // Returns all vehicle-type prices for a route — the quoting endpoint
        [HttpGet("quote")]
        public async Task<IActionResult> Quote([FromQuery] int fromPlaceId, [FromQuery] int toPlaceId)
        {
            var fromPlace = await _db.Places.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PlaceId == fromPlaceId && p.OrgId == Org);
            var toPlace = await _db.Places.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PlaceId == toPlaceId && p.OrgId == Org);

            if (fromPlace is null) return ApiNotFound($"Place {fromPlaceId} not found");
            if (toPlace   is null) return ApiNotFound($"Place {toPlaceId} not found");

            if (!fromPlace.PickupAllowed)
                return ApiBadRequest(fromPlace.RestrictionMessage
                    ?? "Pickup is not available from this location");

            var rates = await _db.RouteRates
                .AsNoTracking()
                .Where(r => r.FromPlaceId == fromPlaceId
                         && r.ToPlaceId   == toPlaceId
                         && r.IsActive
                         && r.OrgId == Org)
                .OrderBy(r => r.VehicleType)
                .Select(r => new
                {
                    r.RouteRateId,
                    r.VehicleType,
                    r.Price,
                    r.PeakPrice,
                })
                .ToListAsync();

            return ApiOk(new
            {
                from   = new { fromPlace.PlaceId, fromPlace.Name },
                to     = new { toPlace.PlaceId,   toPlace.Name   },
                rates,
            });
        }

        // GET api/routerates/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var rate = await _db.RouteRates
                .AsNoTracking()
                .Include(r => r.FromPlace)
                .Include(r => r.ToPlace)
                .FirstOrDefaultAsync(r => r.RouteRateId == id && r.OrgId == Org);

            return rate is null
                ? ApiNotFound($"Route rate {id} not found")
                : ApiOk(rate);
        }

        // POST api/routerates
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] RouteRateUpsertDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.VehicleType))
                return ApiBadRequest("VehicleType is required");
            if (dto.Price <= 0)
                return ApiBadRequest("Price must be greater than zero");

            bool fromExists = await _db.Places.AnyAsync(p => p.PlaceId == dto.FromPlaceId && p.OrgId == Org);
            bool toExists   = await _db.Places.AnyAsync(p => p.PlaceId == dto.ToPlaceId   && p.OrgId == Org);
            if (!fromExists) return ApiBadRequest($"FromPlace {dto.FromPlaceId} not found");
            if (!toExists)   return ApiBadRequest($"ToPlace {dto.ToPlaceId} not found");

            bool duplicate = await _db.RouteRates.AnyAsync(r =>
                r.FromPlaceId == dto.FromPlaceId &&
                r.ToPlaceId   == dto.ToPlaceId   &&
                r.VehicleType == dto.VehicleType  &&
                r.OrgId == Org);
            if (duplicate)
                return ApiBadRequest($"A rate for this route and vehicle type already exists");

            var rate = new RouteRate
            {
                FromPlaceId  = dto.FromPlaceId,
                ToPlaceId    = dto.ToPlaceId,
                VehicleType  = dto.VehicleType.Trim(),
                Price        = dto.Price,
                PeakPrice    = dto.PeakPrice,
                FloorPrice   = dto.FloorPrice,
                IsActive     = dto.IsActive ?? true,
                OrgId        = Org,
            };

            _db.RouteRates.Add(rate);
            await _db.SaveChangesAsync();

            return ApiCreated(rate, "Route rate created");
        }

        // PUT api/routerates/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] RouteRateUpsertDTO dto)
        {
            var rate = await _db.RouteRates
                .FirstOrDefaultAsync(r => r.RouteRateId == id && r.OrgId == Org);

            if (rate is null) return ApiNotFound($"Route rate {id} not found");

            if (dto.Price > 0)                             rate.Price       = dto.Price;
            if (dto.PeakPrice.HasValue)                    rate.PeakPrice   = dto.PeakPrice;
            if (dto.FloorPrice.HasValue)                   rate.FloorPrice  = dto.FloorPrice;
            if (!string.IsNullOrWhiteSpace(dto.VehicleType)) rate.VehicleType = dto.VehicleType.Trim();
            if (dto.IsActive.HasValue)                     rate.IsActive    = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return ApiOk(rate, "Route rate updated");
        }
    }

    public class RouteRateUpsertDTO
    {
        public int      FromPlaceId { get; set; }
        public int      ToPlaceId   { get; set; }
        public string   VehicleType { get; set; } = string.Empty;
        public decimal  Price       { get; set; }
        public decimal? PeakPrice   { get; set; }
        public decimal? FloorPrice  { get; set; }
        public bool?    IsActive    { get; set; }
    }
}
