using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class TravelAgentsRepository : ITravelAgentsRepository
    {
        private readonly AppDbContext _context;

        public TravelAgentsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<TravelAgent?> GetByIdAsync(int id)
        {
            return await _context.TravelAgents.FindAsync(id);
        }

        public async Task<List<TravelAgent>> GetAllAgentsAsync()
        {
            return await _context.TravelAgents.ToListAsync();
        }

        public async Task<List<AgentDashboardDTO>> GetAllAgentsDashboardAsync()
        {
            var agents = await _context.TravelAgents.ToListAsync();
            var allocations = await _context.BookingPaymentAllocations
                .Where(a => a.PayerType == PayerType.Agent || a.PayerType == PayerType.Owner)
                .ToListAsync();

            return agents.Select(agent =>
            {
                var agentAllocs = allocations.Where(a => a.TravelAgentId == agent.AgentId);
                int bookingCount = _context.Bookings.Count(b => b.TravelAgentId == agent.AgentId);
                decimal totalAllocated = agentAllocs.Sum(a => a.AllocatedAmount);
                decimal totalPaid = agentAllocs.Sum(a => a.PaidAmount);

                return new AgentDashboardDTO
                {
                    AgentId          = agent.AgentId,
                    Name             = agent.Name,
                    type             = agent.type,
                    BookingCount     = bookingCount,
                    Earned           = totalPaid,
                    Pending          = Math.Max(0, totalAllocated - totalPaid),
                    ContactNumber    = agent.ContactNumber,
                    Email            = agent.Email,
                    ContactPerson    = agent.ContactPerson,
                    WhatsApp         = agent.WhatsApp,
                    Address          = agent.Address,
                    CommissionPercent = agent.CommissionPercent,
                    PaymentTerms     = agent.PaymentTerms,
                    Notes            = agent.Notes,
                    IsActive         = agent.IsActive,
                };
            }).ToList();
        }

        private static TravelAgentType ParseAgentType(string? raw) => raw switch
        {
            "TravelOwner"    => TravelAgentType.TravelOwner,
            "Hotel"          => TravelAgentType.Hotel,
            "TourOperator"   => TravelAgentType.TourOperator,
            "TravelAgency"   => TravelAgentType.TravelAgency,
            "OnlinePlatform" => TravelAgentType.OnlinePlatform,
            "Individual"     => TravelAgentType.Individual,
            _                => TravelAgentType.Agent
        };

        public async Task<TravelAgent> AddAgent(AddAgentDTO dto)
        {
            var agent = new TravelAgent
            {
                Name             = dto.Name,
                ContactNumber    = dto.ContactNumber,
                type             = ParseAgentType(dto.AgentType),
                Email            = dto.Email,
                ContactPerson    = dto.ContactPerson,
                WhatsApp         = dto.WhatsApp,
                Address          = dto.Address,
                CommissionPercent = dto.CommissionPercent,
                PaymentTerms     = dto.PaymentTerms,
                BankAccount      = dto.BankAccount,
                IFSC             = dto.IFSC,
                Notes            = dto.Notes,
                IsActive         = true,
            };
            await _context.TravelAgents.AddAsync(agent);
            await _context.SaveChangesAsync();
            return agent;
        }

        public async Task<TravelAgent?> UpdateAgent(int id, AddAgentDTO dto)
        {
            var agent = await _context.TravelAgents.FindAsync(id);
            if (agent == null) return null;

            agent.Name             = dto.Name;
            agent.ContactNumber    = dto.ContactNumber;
            agent.type             = ParseAgentType(dto.AgentType);
            agent.Email            = dto.Email;
            agent.ContactPerson    = dto.ContactPerson;
            agent.WhatsApp         = dto.WhatsApp;
            agent.Address          = dto.Address;
            agent.CommissionPercent = dto.CommissionPercent;
            agent.PaymentTerms     = dto.PaymentTerms;
            agent.BankAccount      = dto.BankAccount;
            agent.IFSC             = dto.IFSC;
            agent.Notes            = dto.Notes;
            agent.IsActive         = dto.IsActive;

            await _context.SaveChangesAsync();
            return agent;
        }

        public async Task<decimal> ApplyAgentPayment(AddAgentPaymentDto dto)
        {
            var unpaidBookings = await _context.Bookings
                .Where(b => b.TravelAgentId == dto.AgentId)
                .Select(b => new { b.BookingId, b.travelDate })
                .OrderBy(b => b.travelDate)
                .ToListAsync();

            decimal remaining = dto.TotalPaidAmount;
            decimal applied = 0;

            foreach (var booking in unpaidBookings)
            {
                var allocation = await _context.BookingPaymentAllocations
                    .FirstOrDefaultAsync(a => a.BookingId == booking.BookingId
                        && a.TravelAgentId == dto.AgentId
                        && a.PayerType == PayerType.Agent);

                if (allocation == null) continue;

                decimal pending = allocation.AllocatedAmount - allocation.PaidAmount;
                if (pending <= 0 || remaining <= 0) continue;

                decimal toApply = Math.Min(remaining, pending);

                _context.Payments.Add(new Payments
                {
                    AmountPaid = toApply,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Cash",
                    BookingId = booking.BookingId,
                    TravelAgentId = dto.AgentId
                });

                var allocationRecord = await _context.BookingPaymentAllocations
                    .FirstOrDefaultAsync(a => a.BookingId == booking.BookingId && a.TravelAgentId == dto.AgentId);
                if (allocationRecord != null)
                    allocationRecord.PaidAmount += toApply;

                remaining -= toApply;
                applied += toApply;
            }

            await _context.SaveChangesAsync();
            return applied;
        }

        public async Task<List<Booking>> GetAgentBookingsById(int agentId)
        {
            return await _context.Bookings
                .Where(b => b.TravelAgentId == agentId)
                .OrderByDescending(b => b.travelDate)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetAgentReportBookingsById(int agentId, DateOnly? fromDate, DateOnly? toDate)
        {
            var query = _context.Bookings
                .Include(b => b.TravelAgent)
                .Include(b => b.Vehicle)
                .Include(b => b.Customer)
                .Where(b => b.TravelAgentId == agentId);

            if (fromDate.HasValue && toDate.HasValue)
                query = query.Where(b => b.travelDate >= fromDate.Value && b.travelDate <= toDate.Value);

            return await query.OrderByDescending(b => b.travelDate).ToListAsync();
        }
    }
}