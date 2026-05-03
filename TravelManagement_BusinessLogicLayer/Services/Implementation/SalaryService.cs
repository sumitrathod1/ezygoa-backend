using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class SalaryService : ISalaryService
    {
        private readonly ISalaryRepository _repo;

        public SalaryService(ISalaryRepository repo)
        {
            _repo = repo;
        }

        public Task<List<Salary>> GetAllAsync() => _repo.GetAllAsync();
        public Task<List<Salary>> GetByUserAsync(int userId) => _repo.GetByUserAsync(userId);
        public Task<Salary?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
        public Task<Salary> CreateAsync(Salary salary) => _repo.CreateAsync(salary);
        public Task<Salary?> MarkPaidAsync(int id, DateTime paidDate, string? notes) => _repo.MarkPaidAsync(id, paidDate, notes);
        public Task<List<Salary>> GenerateMonthAsync(int month, int year) => _repo.GenerateMonthAsync(month, year);
    }
}
