using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface ITravelAgentsRepository
    {
        Task<TravelAgent?> GetByIdAsync(int id);
        Task<List<TravelAgent>> GetAllAgentsAsync();
        Task<List<AgentDashboardDTO>> GetAllAgentsDashboardAsync();
        Task<TravelAgent> AddAgent(AddAgentDTO addAgentDTO);
        Task<TravelAgent?> UpdateAgent(int id, AddAgentDTO dto);
        Task<decimal> ApplyAgentPayment(AddAgentPaymentDto addAgentPaymentDto);
        Task<List<Booking>> GetAgentBookingsById(int agentId);
        Task<List<Booking>> GetAgentReportBookingsById(int agentId, DateOnly? from, DateOnly? to);
    }
}