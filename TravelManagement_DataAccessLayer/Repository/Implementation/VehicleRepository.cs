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

        public async Task<VehicleExpence> AddExpense(AddVehicleExpenceDTO dto)
        {
            Category category = dto.CategoryType switch
            {
                "Fuel" => Category.Fuel,
                "Towing" => Category.Towing,
                "DocumentRenew" => Category.DocumentRenew,
                _ => Category.Repair
            };

            var expense = new VehicleExpence
            {
                ExpenseDate = DateTime.Now,
                Amount = dto.Amount,
                CategoryType = category,
                VehicleID = dto.VehicleID
            };
            await _context.AddAsync(expense);
            await _context.SaveChangesAsync();
            return expense;
        }

        public async Task<List<VehicleExpence>> GetAllExpensesAsync()
        {
            return await _context.vehicleExpences.Include(g => g.Vehicle).ToListAsync();
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