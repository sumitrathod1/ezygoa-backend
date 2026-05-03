using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class TravelAgentsService : ITravelAgentsService
    {
        private readonly ITravelAgentsRepository _agentRepo;

        public TravelAgentsService(ITravelAgentsRepository agentRepo)
        {
            _agentRepo = agentRepo;
        }

        public Task<TravelAgent> AddAgentAsync(AddAgentDTO dto) => _agentRepo.AddAgent(dto);

        public Task<TravelAgent?> UpdateAgentAsync(int id, AddAgentDTO dto) => _agentRepo.UpdateAgent(id, dto);

        public Task<List<AgentDashboardDTO>> GetAllAgentsDashboardAsync() => _agentRepo.GetAllAgentsDashboardAsync();

        public Task<decimal> ApplyAgentPaymentAsync(AddAgentPaymentDto dto) => _agentRepo.ApplyAgentPayment(dto);

        public Task<TravelAgent?> GetAgentByIdAsync(int id) => _agentRepo.GetByIdAsync(id);

        public Task<List<Booking>> GetAgentBookingsByIdAsync(int id) => _agentRepo.GetAgentBookingsById(id);

        public Task<List<Booking>> GetAgentReportBookingsByIdAsync(int agentId, DateOnly? from, DateOnly? to) =>
            _agentRepo.GetAgentReportBookingsById(agentId, from, to);
    }
}