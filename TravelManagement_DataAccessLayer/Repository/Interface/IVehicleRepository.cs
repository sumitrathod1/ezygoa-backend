using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IVehicleRepository
    {
        Task<List<Vehicle>> GetAllVehiclesAsync();
        Task<Vehicle?> GetVehicleByIdAsync(int id);
        Task<bool> VehicleNumberExistsAsync(string vehicleNumber);
        Task<Vehicle> AddVehcle(Vehicle vehicle);
        Task<Vehicle> UpdateVechicle(Vehicle vehicle);
        Task<VehicleExpence> AddExpense(AddVehicleExpenceDTO addVehicleExpenceDTO);
        Task<VehicleExpence?> UpdateExpenseAsync(int id, AddVehicleExpenceDTO dto);
        Task<bool> DeleteExpenseAsync(int id);
        Task<List<VehicleExpence>> GetAllExpensesAsync();
        Task<List<VehicleExpence>> GetFilteredExpensesAsync(int? vehicleId, string? type, DateTime? startDate, DateTime? endDate);
        Task<List<CombinedExpenseDTO>> GetCombinedExpensesAsync(int? vehicleId, string? type, DateTime? startDate, DateTime? endDate);
        Task<object> GetExpenseSummaryAsync(int? vehicleId, DateTime? startDate, DateTime? endDate);
        Task<VehicleExpence?> GetExpenseByVehicleNumberAsync(string vehicleNumber);
        Task<Documents> AddDocumentAsync(Documents document);
        Task<List<Documents>> GetAllDocumentsAsync();
        Task<Documents?> UpdateDocumentAsync(Documents document);
        Task<Documents?> GetDocumentByIdAsync(int id);
        Task<bool> DeleteDocumentAsync(int id);
        Task<VehicleMaintenanceShedule?> AddNewVechicleMaintenance(VechicleMaintenanceDTO dto);
        Task<List<VehicleMaintenanceShedule>> GetMaintenanceShedule();
    }
}