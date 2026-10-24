using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [Authorize]   // every read here was anonymous (vehicles, expenses, documents, EMI) across all orgs
    [EnableRateLimiting("api")]
    public class VehicleController : ApiControllerBase
    {
        private readonly IVehicleService _vehicleService;
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public VehicleController(IVehicleService vehicleService, AppDbContext context, TenantContext tenant)
        {
            _vehicleService = vehicleService;
            _context = context;
            _tenant  = tenant;
        }

        [HttpGet("GetAllVehicles")]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> GetAllVehicles()
        {
            var vehicles = await _vehicleService.GetAllVehiclesAsync();
            return ApiOk(vehicles);
        }

        [HttpGet("GetVehicle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetVehicle(int id)
        {
            var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
            return vehicle is null
                ? ApiNotFound($"Vehicle with ID {id} not found")
                : ApiOk(vehicle);
        }

        [HttpPost("AddVehcle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddVehicle([FromBody] Vehicle vehicle)
        {
            if (vehicle is null) return ApiBadRequest("Vehicle data is required");
            var result = await _vehicleService.AddVehicleAsync(vehicle);
            return ApiCreated(result, "Vehicle added successfully");
        }

        [HttpPut("UpdateVehicle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVehicle([FromBody] Vehicle vehicle)
        {
            if (vehicle is null) return ApiBadRequest("Vehicle data is required");
            var updated = await _vehicleService.UpdateVehicleAsync(vehicle);
            return updated is null
                ? ApiNotFound("Vehicle not found")
                : ApiOk(updated, "Vehicle updated successfully");
        }

        [HttpPost("AddVehicleExpence")]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<IActionResult> AddVehicleExpense([FromBody] AddVehicleExpenceDTO dto)
        {
            if (dto is null) return ApiBadRequest("Expense data is required");
            var result = await _vehicleService.AddExpenseAsync(dto);
            return ApiCreated(result, "Expense recorded successfully");
        }

        [HttpPut("UpdateExpense/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] AddVehicleExpenceDTO dto)
        {
            if (dto is null) return ApiBadRequest("Expense data is required");
            var result = await _vehicleService.UpdateExpenseAsync(id, dto);
            return result is null
                ? ApiNotFound($"Expense {id} not found")
                : ApiOk(result, "Expense updated successfully");
        }

        [HttpDelete("DeleteExpense/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var deleted = await _vehicleService.DeleteExpenseAsync(id);
            return deleted
                ? ApiOk<object>(null, "Expense deleted successfully")
                : ApiNotFound($"Expense {id} not found");
        }

        [HttpGet("GetAllexpence")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllExpenses()
        {
            var result = await _vehicleService.GetAllExpensesAsync();
            return ApiOk(result);
        }

        [HttpGet("GetFilteredExpenses")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetFilteredExpenses(
            [FromQuery] int? vehicleId,
            [FromQuery] string? type,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var result = await _vehicleService.GetFilteredExpensesAsync(vehicleId, type, startDate, endDate);
            return ApiOk(result);
        }

        [HttpGet("GetCombinedExpenses")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetCombinedExpenses(
            [FromQuery] int? vehicleId,
            [FromQuery] string? type,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var result = await _vehicleService.GetCombinedExpensesAsync(vehicleId, type, startDate, endDate);
            return ApiOk(result);
        }

        [HttpGet("GetExpenseSummary")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetExpenseSummary(
            [FromQuery] int? vehicleId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var result = await _vehicleService.GetExpenseSummaryAsync(vehicleId, startDate, endDate);
            return ApiOk(result);
        }

        [HttpGet("GetExpenceBybId")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetExpenseByVehicleNumber(string vehicleNumber)
        {
            var result = await _vehicleService.GetExpenseByVehicleNumberAsync(vehicleNumber);
            return result is null
                ? ApiNotFound($"No expense found for vehicle '{vehicleNumber}'")
                : ApiOk(result);
        }

        [HttpPost("AddDocumentDetails")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddDocument([FromBody] Documents documents)
        {
            if (documents is null) return ApiBadRequest("Document data is required");
            var result = await _vehicleService.AddDocumentAsync(documents);
            return ApiCreated(result, "Document added successfully");
        }

        [HttpGet("GetAlldocuments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllDocuments()
        {
            var result = await _vehicleService.GetAllDocumentsAsync();
            return ApiOk(result);
        }

        [HttpPut("UpdateDocument")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateDocument([FromBody] Documents documents)
        {
            if (documents is null) return ApiBadRequest("Document data is required");
            var result = await _vehicleService.UpdateDocumentAsync(documents);
            return result is null
                ? ApiNotFound("Document not found")
                : ApiOk(result, "Document updated successfully");
        }

        [HttpGet("GetDocumentById")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDocumentById(int id)
        {
            var document = await _vehicleService.GetDocumentByIdAsync(id);
            return document is null ? ApiNotFound($"Document with ID {id} not found") : ApiOk(document);
        }

        [HttpDelete("DeleteDocument/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            var deleted = await _vehicleService.DeleteDocumentAsync(id);
            return deleted ? ApiOk<object>(null, "Document deleted successfully") : ApiNotFound($"Document with ID {id} not found");
        }

        [HttpPost("AddVechicleMaintenance")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddVehicleMaintenance([FromBody] VechicleMaintenanceDTO dto)
        {
            var result = await _vehicleService.AddMaintenanceAsync(dto);
            return result is null
                ? ApiNotFound("Vehicle not found or invalid maintenance data")
                : ApiCreated(result, "Maintenance schedule added successfully");
        }

        [HttpGet("GetVehicleMaintenance")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetVehicleMaintenance()
        {
            var result = await _vehicleService.GetMaintenanceScheduleAsync();
            return ApiOk(result);
        }

        [HttpGet("{id}/emi-statement")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetEmiStatement(int id)
        {
            var vehicle = await _context.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(v => v.VehicleId == id);
            if (vehicle == null || !_tenant.CanAccess(vehicle.OrgId)) return ApiNotFound($"Vehicle {id} not found");

            var payments = await _context.vehicleExpences
                .AsNoTracking()
                .Where(e => e.VehicleID == id && e.CategoryType == Category.EMI)
                .OrderBy(e => e.ExpenseDate)
                .Select(e => new
                {
                    e.VehicleExpenceId,
                    e.ExpenseDate,
                    e.Amount,
                    e.Notes,
                })
                .ToListAsync();

            return ApiOk(new
            {
                vehicleId     = vehicle.VehicleId,
                vehicleName   = vehicle.VehicleName,
                vehicleNumber = vehicle.VehicleNumber,
                emiLender     = vehicle.EMILender,
                emiAmount     = vehicle.EMIAmount,
                emiDay        = vehicle.EMIDay,
                emiStartDate  = vehicle.EMIStartDate,
                emiEndDate    = vehicle.EMIEndDate,
                totalEMIs     = vehicle.TotalEMIs,
                paidEMIs      = vehicle.PaidEMIs,
                totalPaid     = payments.Sum(p => p.Amount),
                payments,
            });
        }

        [HttpGet("available")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAvailableVehicles([FromQuery] DateOnly? date = null)
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);

            // Was every organisation's vehicles and bookings
            var allVehicles = await _context.Vehicles
                .Where(v => !_tenant.ShouldFilter || v.OrgId == _tenant.OrgId)
                .ToListAsync();

            var busyIds = await _context.Bookings
                .Where(b => b.travelDate == targetDate && b.Status != Status.Canceled && b.VehicleId != null
                         && (!_tenant.ShouldFilter || b.OrgId == _tenant.OrgId))
                .Select(b => b.VehicleId!.Value)
                .Distinct()
                .ToListAsync();

            var available = allVehicles
                .Select(v => new
                {
                    v.VehicleId, v.VehicleName, v.VehicleNumber,
                    v.VehicleType, v.Seatingcapacity,
                    IsFree = !busyIds.Contains(v.VehicleId),
                })
                .Where(v => v.IsFree)
                .ToList();

            return ApiOk(available);
        }
    }
}