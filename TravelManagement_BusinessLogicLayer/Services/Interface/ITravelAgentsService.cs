using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface ITravelAgentsService
    {
        Task<TravelAgent> AddAgentAsync(AddAgentDTO dto);
        Task<TravelAgent?> UpdateAgentAsync(int id, AddAgentDTO dto);
        Task<List<AgentDashboardDTO>> GetAllAgentsDashboardAsync();
        Task<decimal> ApplyAgentPaymentAsync(AddAgentPaymentDto dto);
        Task<TravelAgent?> GetAgentByIdAsync(int id);
        Task<List<Booking>> GetAgentBookingsByIdAsync(int id);
        Task<List<Booking>> GetAgentReportBookingsByIdAsync(int agentId, DateOnly? from, DateOnly? to);
    }
}