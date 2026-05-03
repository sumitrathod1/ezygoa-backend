using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface ISalaryRepository
    {
        Task<List<Salary>> GetAllAsync();
        Task<List<Salary>> GetByUserAsync(int userId);
        Task<Salary?> GetByIdAsync(int id);
        Task<Salary> CreateAsync(Salary salary);
        Task<Salary?> MarkPaidAsync(int id, DateTime paidDate, string? notes);
        Task<bool> ExistsAsync(int userId, int month, int year);
        Task<List<Salary>> GenerateMonthAsync(int month, int year);
    }
}
