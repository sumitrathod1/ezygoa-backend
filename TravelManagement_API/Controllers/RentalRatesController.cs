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
    public class RentalRatesController : ApiControllerBase
    {
        private readonly AppDbContext _db;

        public RentalRatesController(AppDbContext db) => _db = db;

        // GET api/rentalrates
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var query = _db.RentalRates
                .AsNoTracking()
                .Include(r => r.Vehicle)
                .Where(r => r.OrgId == 1);

            if (!includeInactive) query = query.Where(r => r.IsActive);

            var rates = await query
                .OrderBy(r => r.Vehicle!.VehicleName)
                .ToListAsync();

            return ApiOk(rates);
        }

        // GET api/rentalrates/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var rate = await _db.RentalRates
                .AsNoTracking()
                .Include(r => r.Vehicle)
                .FirstOrDefaultAsync(r => r.RentalRateId == id && r.OrgId == 1);

            return rate is null
                ? ApiNotFound($"Rental rate {id} not found")
                : ApiOk(rate);
        }

        // POST api/rentalrates
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] RentalRateUpsertDTO dto)
        {
            if (dto.PricePerDay <= 0)
                return ApiBadRequest("PricePerDay must be greater than zero");
            if (dto.SecurityDeposit < 0)
                return ApiBadRequest("SecurityDeposit cannot be negative");

            bool vehicleExists = await _db.Vehicles.AnyAsync(v => v.VehicleId == dto.VehicleId && v.OrgId == 1);
            if (!vehicleExists)
                return ApiBadRequest($"Vehicle {dto.VehicleId} not found");

            bool duplicate = await _db.RentalRates
                .AnyAsync(r => r.VehicleId == dto.VehicleId && r.OrgId == 1);
            if (duplicate)
                return ApiBadRequest("A rental rate for this vehicle already exists");

            var rate = new RentalRate
            {
                VehicleId       = dto.VehicleId,
                PricePerDay     = dto.PricePerDay,
                WeeklyRate      = dto.WeeklyRate,
                SecurityDeposit = dto.SecurityDeposit,
                MinDays         = dto.MinDays < 1 ? 1 : dto.MinDays,
                KmPerDay        = dto.KmPerDay,
                ExtraKmRate     = dto.ExtraKmRate,
                IsActive        = dto.IsActive ?? true,
                OrgId           = 1,
            };

            _db.RentalRates.Add(rate);
            await _db.SaveChangesAsync();

            return ApiCreated(rate, "Rental rate created");
        }

        // PUT api/rentalrates/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] RentalRateUpsertDTO dto)
        {
            var rate = await _db.RentalRates
                .FirstOrDefaultAsync(r => r.RentalRateId == id && r.OrgId == 1);

            if (rate is null) return ApiNotFound($"Rental rate {id} not found");

            if (dto.PricePerDay > 0)     rate.PricePerDay     = dto.PricePerDay;
            if (dto.SecurityDeposit >= 0) rate.SecurityDeposit = dto.SecurityDeposit;
            if (dto.WeeklyRate.HasValue)  rate.WeeklyRate      = dto.WeeklyRate;
            if (dto.KmPerDay.HasValue)    rate.KmPerDay        = dto.KmPerDay;
            if (dto.ExtraKmRate.HasValue) rate.ExtraKmRate     = dto.ExtraKmRate;
            if (dto.MinDays >= 1)         rate.MinDays         = dto.MinDays;
            if (dto.IsActive.HasValue)    rate.IsActive        = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return ApiOk(rate, "Rental rate updated");
        }
    }

    public class RentalRateUpsertDTO
    {
        public int      VehicleId       { get; set; }
        public decimal  PricePerDay     { get; set; }
        public decimal? WeeklyRate      { get; set; }
        public decimal  SecurityDeposit { get; set; }
        public int      MinDays         { get; set; } = 1;
        public int?     KmPerDay        { get; set; }
        public decimal? ExtraKmRate     { get; set; }
        public bool?    IsActive        { get; set; }
    }
}
