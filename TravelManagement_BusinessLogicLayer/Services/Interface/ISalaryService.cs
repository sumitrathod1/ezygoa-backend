using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface ISalaryService
    {
        Task<List<Salary>> GetAllAsync();
        Task<List<Salary>> GetByUserAsync(int userId);
        Task<Salary?> GetByIdAsync(int id);
        Task<Salary> CreateAsync(Salary salary);
        Task<Salary?> MarkPaidAsync(int id, DateTime paidDate, string? notes);
        Task<List<Salary>> GenerateMonthAsync(int month, int year);
    }
}
