using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class RateChartService : IRateChartService
    {
        private readonly IRateChartRepository _repo;

        public RateChartService(IRateChartRepository repo)
        {
            _repo = repo;
        }

        public Task<List<RateChart>> GetAllAsync() => _repo.GetAllAsync();
        public Task<RateChart?> GetByIdAsync(string id) => _repo.GetByIdAsync(id);
        public Task<RateChart> CreateAsync(RateChart chart) => _repo.CreateAsync(chart);
        public Task<RateChart?> UpdateAsync(string id, RateChart chart) => _repo.UpdateAsync(id, chart);
        public Task<bool> DeleteAsync(string id) => _repo.DeleteAsync(id);
    }
}
