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
        Task<List<VehicleExpence>> GetAllExpensesAsync();
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