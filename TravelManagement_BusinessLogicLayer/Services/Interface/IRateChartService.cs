using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface IRateChartService
    {
        Task<List<RateChart>> GetAllAsync();
        Task<RateChart?> GetByIdAsync(string id);
        Task<RateChart> CreateAsync(RateChart chart);
        Task<RateChart?> UpdateAsync(string id, RateChart chart);
        Task<bool> DeleteAsync(string id);
    }
}
