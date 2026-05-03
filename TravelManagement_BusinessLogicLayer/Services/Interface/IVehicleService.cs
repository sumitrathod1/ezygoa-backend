using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface IVehicleService
    {
        Task<List<Vehicle>> GetAllVehiclesAsync();
        Task<Vehicle?> GetVehicleByIdAsync(int id);
        Task<Vehicle> AddVehicleAsync(Vehicle vehicle);
        Task<Vehicle?> UpdateVehicleAsync(Vehicle vehicle);
        Task<VehicleExpence> AddExpenseAsync(AddVehicleExpenceDTO dto);
        Task<List<VehicleExpence>> GetAllExpensesAsync();
        Task<VehicleExpence?> GetExpenseByVehicleNumberAsync(string vehicleNumber);
        Task<Documents> AddDocumentAsync(Documents document);
        Task<List<Documents>> GetAllDocumentsAsync();
        Task<Documents?> UpdateDocumentAsync(Documents document);
        Task<Documents?> GetDocumentByIdAsync(int id);
        Task<bool> DeleteDocumentAsync(int id);
        Task<VehicleMaintenanceShedule?> AddMaintenanceAsync(VechicleMaintenanceDTO dto);
        Task<List<VehicleMaintenanceShedule>> GetMaintenanceScheduleAsync();
    }
}