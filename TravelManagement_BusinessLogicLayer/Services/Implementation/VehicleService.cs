using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class VehicleService : IVehicleService
    {
        private readonly IVehicleRepository _vehicleRepo;

        public VehicleService(IVehicleRepository vehicleRepo)
        {
            _vehicleRepo = vehicleRepo;
        }

        public Task<List<Vehicle>> GetAllVehiclesAsync() => _vehicleRepo.GetAllVehiclesAsync();

        public Task<Vehicle?> GetVehicleByIdAsync(int id) => _vehicleRepo.GetVehicleByIdAsync(id);

        public async Task<Vehicle> AddVehicleAsync(Vehicle vehicle)
        {
            if (await _vehicleRepo.VehicleNumberExistsAsync(vehicle.VehicleNumber!))
                throw new InvalidOperationException("Vehicle is already registered");
            return await _vehicleRepo.AddVehcle(vehicle);
        }

        public async Task<Vehicle?> UpdateVehicleAsync(Vehicle vehicle)
        {
            return await _vehicleRepo.UpdateVechicle(vehicle);
        }

        public Task<VehicleExpence> AddExpenseAsync(AddVehicleExpenceDTO dto) => _vehicleRepo.AddExpense(dto);

        public Task<VehicleExpence?> UpdateExpenseAsync(int id, AddVehicleExpenceDTO dto) =>
            _vehicleRepo.UpdateExpenseAsync(id, dto);

        public Task<bool> DeleteExpenseAsync(int id) => _vehicleRepo.DeleteExpenseAsync(id);

        public Task<List<VehicleExpence>> GetAllExpensesAsync() => _vehicleRepo.GetAllExpensesAsync();

        public Task<List<VehicleExpence>> GetFilteredExpensesAsync(int? vehicleId, string? type, DateTime? startDate, DateTime? endDate) =>
            _vehicleRepo.GetFilteredExpensesAsync(vehicleId, type, startDate, endDate);

        public Task<object> GetExpenseSummaryAsync(int? vehicleId, DateTime? startDate, DateTime? endDate) =>
            _vehicleRepo.GetExpenseSummaryAsync(vehicleId, startDate, endDate);

        public Task<VehicleExpence?> GetExpenseByVehicleNumberAsync(string vehicleNumber) =>
            _vehicleRepo.GetExpenseByVehicleNumberAsync(vehicleNumber);

        public Task<Documents> AddDocumentAsync(Documents document) => _vehicleRepo.AddDocumentAsync(document);

        public Task<List<Documents>> GetAllDocumentsAsync() => _vehicleRepo.GetAllDocumentsAsync();

        public Task<Documents?> UpdateDocumentAsync(Documents document) => _vehicleRepo.UpdateDocumentAsync(document);

        public Task<Documents?> GetDocumentByIdAsync(int id) => _vehicleRepo.GetDocumentByIdAsync(id);

        public Task<bool> DeleteDocumentAsync(int id) => _vehicleRepo.DeleteDocumentAsync(id);

        public Task<VehicleMaintenanceShedule?> AddMaintenanceAsync(VechicleMaintenanceDTO dto) =>
            _vehicleRepo.AddNewVechicleMaintenance(dto);

        public Task<List<VehicleMaintenanceShedule>> GetMaintenanceScheduleAsync() => _vehicleRepo.GetMaintenanceShedule();
    }
}