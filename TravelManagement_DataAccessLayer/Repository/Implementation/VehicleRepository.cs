using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly AppDbContext _context;

        public VehicleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            return await _context.Vehicles.ToListAsync();
        }

        public async Task<Vehicle?> GetVehicleByIdAsync(int id)
        {
            return await _context.Vehicles.FindAsync(id);
        }

        public async Task<bool> VehicleNumberExistsAsync(string vehicleNumber)
        {
            return await _context.Vehicles.AnyAsync(x => x.VehicleNumber == vehicleNumber);
        }

        public async Task<Vehicle> AddVehcle(Vehicle vehicle)
        {
            var newVehicle = new Vehicle
            {
                VehicleName = vehicle.VehicleName,
                VehicleNumber = vehicle.VehicleNumber,
                VehicleType = vehicle.VehicleType,
                RegistrationDate = vehicle.RegistrationDate,
                Seatingcapacity = vehicle.Seatingcapacity,
                VehicleAge = Calculations.CalculateAge(vehicle.RegistrationDate, DateTime.Now)
            };
            await _context.AddAsync(newVehicle);
            await _context.SaveChangesAsync();
            return newVehicle;
        }

        public async Task<Vehicle> UpdateVechicle(Vehicle vehicle)
        {
            var existing = await _context.Vehicles.FirstOrDefaultAsync(x => x.VehicleId == vehicle.VehicleId);
            if (existing != null)
            {
                existing.VehicleNumber = vehicle.VehicleNumber;
                existing.VehicleName = vehicle.VehicleName;
                existing.RegistrationDate = vehicle.RegistrationDate;
                existing.VehicleType = vehicle.VehicleType;
                existing.Seatingcapacity = vehicle.Seatingcapacity;
                existing.HasEMI = vehicle.HasEMI;
                existing.EMIAmount = vehicle.EMIAmount;
                existing.EMIDay = vehicle.EMIDay;
                existing.EMIStartDate = vehicle.EMIStartDate;
                existing.EMIEndDate = vehicle.EMIEndDate;
                existing.EMILender = vehicle.EMILender;
                existing.TotalEMIs = vehicle.TotalEMIs;
                existing.PaidEMIs = vehicle.PaidEMIs;
                await _context.SaveChangesAsync();
            }
            return vehicle;
        }

        private static Category ParseCategory(string type) => type switch
        {
            "Fuel"         => Category.Fuel,
            "Towing"       => Category.Towing,
            "DocumentRenew"=> Category.DocumentRenew,
            "Salary"       => Category.Salary,
            "EMI"          => Category.EMI,
            "Insurance"    => Category.Insurance,
            "Service"      => Category.Service,
            "Tyre"         => Category.Tyre,
            "Other"        => Category.Other,
            _              => Category.Repair
        };

        public async Task<VehicleExpence> AddExpense(AddVehicleExpenceDTO dto)
        {
            var expense = new VehicleExpence
            {
                ExpenseDate  = dto.ExpenseDate == default ? DateTime.Now : dto.ExpenseDate,
                Amount       = dto.Amount,
                CategoryType = ParseCategory(dto.CategoryType),
                VehicleID    = dto.VehicleID,
                Notes        = dto.Notes
            };
            await _context.AddAsync(expense);
            await _context.SaveChangesAsync();
            return expense;
        }

        public async Task<VehicleExpence?> UpdateExpenseAsync(int id, AddVehicleExpenceDTO dto)
        {
            var expense = await _context.vehicleExpences.FindAsync(id);
            if (expense == null) return null;
            expense.ExpenseDate  = dto.ExpenseDate == default ? expense.ExpenseDate : dto.ExpenseDate;
            expense.Amount       = dto.Amount;
            expense.CategoryType = ParseCategory(dto.CategoryType);
            expense.VehicleID    = dto.VehicleID;   // nullable OK
            expense.Notes        = dto.Notes;
            await _context.SaveChangesAsync();
            return expense;
        }

        public async Task<bool> DeleteExpenseAsync(int id)
        {
            var expense = await _context.vehicleExpences.FindAsync(id);
            if (expense == null) return false;
            _context.vehicleExpences.Remove(expense);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<VehicleExpence>> GetAllExpensesAsync()
        {
            return await _context.vehicleExpences
                .Include(g => g.Vehicle)
                .OrderByDescending(e => e.ExpenseDate)
                .ToListAsync();
        }

        public async Task<List<VehicleExpence>> GetFilteredExpensesAsync(
            int? vehicleId, string? type, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.vehicleExpences.Include(e => e.Vehicle).AsQueryable();
            if (vehicleId.HasValue)
                q = q.Where(e => e.VehicleID != null && e.VehicleID == vehicleId.Value);
            if (!string.IsNullOrEmpty(type))
            {
                var cat = ParseCategory(type);
                q = q.Where(e => e.CategoryType == cat);
            }
            if (startDate.HasValue)
                q = q.Where(e => e.ExpenseDate >= startDate.Value);
            if (endDate.HasValue)
                q = q.Where(e => e.ExpenseDate < endDate.Value.AddDays(1));
            return await q.OrderByDescending(e => e.ExpenseDate).ToListAsync();
        }

        public async Task<List<CombinedExpenseDTO>> GetCombinedExpensesAsync(
            int? vehicleId, string? type, DateTime? startDate, DateTime? endDate)
        {
            bool salaryTypeOnly = string.Equals(type, "Salary", StringComparison.OrdinalIgnoreCase);

            var combined = new List<CombinedExpenseDTO>();

            // --- Vehicle expenses (non-salary) ---
            if (!salaryTypeOnly)
            {
                var q = _context.vehicleExpences.Include(e => e.Vehicle).AsQueryable();
                if (vehicleId.HasValue)
                    q = q.Where(e => e.VehicleID != null && e.VehicleID == vehicleId.Value);
                if (!string.IsNullOrEmpty(type))
                    q = q.Where(e => e.CategoryType == ParseCategory(type));
                else
                    q = q.Where(e => e.CategoryType != Category.Salary);
                if (startDate.HasValue)
                    q = q.Where(e => e.ExpenseDate >= startDate.Value);
                if (endDate.HasValue)
                    q = q.Where(e => e.ExpenseDate < endDate.Value.AddDays(1));

                var vehicleExpenses = await q.ToListAsync();
                combined.AddRange(vehicleExpenses.Select(e => new CombinedExpenseDTO
                {
                    VehicleExpenceId = e.VehicleExpenceId,
                    SalaryId         = null,
                    ExpenseDate      = e.ExpenseDate,
                    Amount           = e.Amount,
                    CategoryType     = e.CategoryType.ToString(),
                    VehicleID        = e.VehicleID,
                    Vehicle          = e.Vehicle == null ? null : new CombinedExpenseVehicle
                    {
                        VehicleName   = e.Vehicle.VehicleName,
                        VehicleNumber = e.Vehicle.VehicleNumber,
                    },
                    Notes          = e.Notes,
                    DriverName     = null,
                    IsSalaryRecord = false,
                }));
            }

            // --- Salary entries: ALL auto-created records count as expenses (no IsPaid filter).
            //     Active drivers' salary records are auto-created; they appear immediately. ---
            bool includeSalaries = !vehicleId.HasValue &&
                (string.IsNullOrEmpty(type) || salaryTypeOnly);

            if (includeSalaries)
            {
                var salaryQ = _context.salaries
                    .Include(s => s.user)
                    .AsQueryable();

                // Filter by the salary's month/year (not PaidDate) since IsPaid is no longer required
                if (startDate.HasValue)
                    salaryQ = salaryQ.Where(s =>
                        new DateTime(s.Year, s.Month, 1) >= startDate.Value.Date);
                if (endDate.HasValue)
                    salaryQ = salaryQ.Where(s =>
                        new DateTime(s.Year, s.Month, 1) < endDate.Value.AddDays(1).Date);

                var salaries = await salaryQ.ToListAsync();
                combined.AddRange(salaries.Select(s => new CombinedExpenseDTO
                {
                    VehicleExpenceId = null,
                    SalaryId         = s.SalaryId,
                    ExpenseDate      = s.PaidDate ?? new DateTime(s.Year, s.Month, 1),
                    Amount           = s.NetSalaey,
                    CategoryType     = "Salary",
                    VehicleID        = null,
                    Vehicle          = null,
                    Notes            = s.Notes,
                    DriverName       = s.user?.EmployeeName,
                    IsSalaryRecord   = true,
                }));
            }

            return combined.OrderByDescending(e => e.ExpenseDate).ToList();
        }

        public async Task<object> GetExpenseSummaryAsync(int? vehicleId, DateTime? startDate, DateTime? endDate)
        {
            var q = _context.vehicleExpences.Include(e => e.Vehicle).AsQueryable();
            if (vehicleId.HasValue)
                q = q.Where(e => e.VehicleID == vehicleId.Value);
            if (startDate.HasValue)
                q = q.Where(e => e.ExpenseDate >= startDate.Value);
            if (endDate.HasValue)
                q = q.Where(e => e.ExpenseDate < endDate.Value.AddDays(1));

            var expenses = await q.ToListAsync();
            var total    = expenses.Sum(e => e.Amount);

            var byType = expenses
                .GroupBy(e => e.CategoryType.ToString())
                .Select(g => new { type = g.Key, total = g.Sum(e => e.Amount), count = g.Count() })
                .OrderByDescending(g => g.total)
                .ToList();

            var byVehicle = expenses
                .GroupBy(e => new { e.VehicleID, name = e.Vehicle?.VehicleName ?? "Unknown" })
                .Select(g => new { vehicleId = g.Key.VehicleID, vehicleName = g.Key.name,
                                   total = g.Sum(e => e.Amount), count = g.Count() })
                .OrderByDescending(g => g.total)
                .ToList();

            return new { total, count = expenses.Count, byType, byVehicle };
        }

        public async Task<VehicleExpence?> GetExpenseByVehicleNumberAsync(string vehicleNumber)
        {
            return await _context.vehicleExpences
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(y => y.Vehicle!.VehicleNumber == vehicleNumber);
        }

        public async Task<Documents> AddDocumentAsync(Documents document)
        {
            await _context.AddAsync(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<List<Documents>> GetAllDocumentsAsync()
        {
            return await _context.Documents.Include(v => v.Vehicle).ToListAsync();
        }

        public async Task<Documents?> UpdateDocumentAsync(Documents document)
        {
            var existing = await _context.Documents.FirstOrDefaultAsync(x => x.DocumentID == document.DocumentID);
            if (existing == null) return null;

            existing.Title = document.Title;
            existing.Description = document.Description;
            existing.ExpiryDate = document.ExpiryDate;
            existing.HasExpiry = document.HasExpiry;
            existing.VehicleID = document.VehicleID;
            existing.DocumentType = document.DocumentType;
            existing.Category = document.Category;
            existing.DocumentNumber = document.DocumentNumber;
            existing.IssuedBy = document.IssuedBy;
            existing.IssueDate = document.IssueDate;
            existing.DriverName = document.DriverName;
            if (document.FileUrl != null) existing.FileUrl = document.FileUrl;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<Documents?> GetDocumentByIdAsync(int id)
        {
            return await _context.Documents.Include(v => v.Vehicle).FirstOrDefaultAsync(d => d.DocumentID == id);
        }

        public async Task<bool> DeleteDocumentAsync(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return false;
            _context.Documents.Remove(doc);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<VehicleMaintenanceShedule?> AddNewVechicleMaintenance(VechicleMaintenanceDTO dto)
        {
            var exists = await _context.Vehicles.FindAsync(dto.VehicleID);
            if (exists == null) return null;

            MaintenanceType mType = dto.maintenanceType switch
            {
                "oilChange" => MaintenanceType.oilChange,
                "TireChange" => MaintenanceType.TireChange,
                _ => MaintenanceType.Service
            };

            var record = new VehicleMaintenanceShedule
            {
                ServieDate = dto.ServieDate,
                Nextduedate = dto.Nextduedate,
                Description = dto.Description,
                cost = dto.cost,
                maintenanceType = mType,
                VehicleID = dto.VehicleID
            };
            await _context.AddAsync(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<List<VehicleMaintenanceShedule>> GetMaintenanceShedule()
        {
            return await _context.vehicleMaintenanceShedules.Include(b => b.Vehicle).ToListAsync();
        }
    }
}