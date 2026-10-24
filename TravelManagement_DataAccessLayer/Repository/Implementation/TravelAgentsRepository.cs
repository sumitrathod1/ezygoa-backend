using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class TravelAgentsRepository : ITravelAgentsRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public TravelAgentsRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        private IQueryable<TravelAgent> OrgAgents =>
            _tenant.ShouldFilter
                ? _context.TravelAgents.Where(a => a.OrgId == _tenant.OrgId)
                : _context.TravelAgents;

        public async Task<TravelAgent?> GetByIdAsync(int id)
        {
            var agent = await _context.TravelAgents.FindAsync(id);
            return agent != null && _tenant.CanAccess(agent.OrgId) ? agent : null;
        }

        public async Task<List<TravelAgent>> GetAllAgentsAsync()
        {
            return await OrgAgents.ToListAsync();
        }

        public async Task<List<AgentDashboardDTO>> GetAllAgentsDashboardAsync()
        {
            var agents    = await OrgAgents.ToListAsync();
            var agentIds  = agents.Select(a => a.AgentId).ToList();

            var allocations = await _context.BookingPaymentAllocations
                .Where(a => agentIds.Contains(a.TravelAgentId ?? 0)
                         && (a.PayerType == PayerType.Agent || a.PayerType == PayerType.Owner)
                         && a.Booking.Status != Status.Canceled)
                .ToListAsync();

            // One grouped query instead of a COUNT per agent
            var bookingCounts = await _context.Bookings
                .Where(b => b.TravelAgentId != null && agentIds.Contains(b.TravelAgentId.Value)
                         && b.Status != Status.Canceled
                         && (!_tenant.ShouldFilter || b.OrgId == _tenant.OrgId))
                .GroupBy(b => b.TravelAgentId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return agents.Select(agent =>
            {
                var agentAllocs = allocations.Where(a => a.TravelAgentId == agent.AgentId);
                int bookingCount = bookingCounts.GetValueOrDefault(agent.AgentId, 0);
                decimal totalAllocated = agentAllocs.Sum(a => a.AllocatedAmount);
                decimal totalPaid      = agentAllocs.Sum(a => a.PaidAmount);

                return new AgentDashboardDTO
                {
                    AgentId           = agent.AgentId,
                    Name              = agent.Name,
                    type              = agent.type,
                    BookingCount      = bookingCount,
                    Earned            = totalPaid,
                    Pending           = Math.Max(0, totalAllocated - totalPaid),
                    ContactNumber     = agent.ContactNumber,
                    Email             = agent.Email,
                    ContactPerson     = agent.ContactPerson,
                    WhatsApp          = agent.WhatsApp,
                    Address           = agent.Address,
                    CommissionPercent = agent.CommissionPercent,
                    PaymentTerms      = agent.PaymentTerms,
                    Notes             = agent.Notes,
                    IsActive          = agent.IsActive,
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
                Name              = dto.Name,
                ContactNumber     = dto.ContactNumber,
                type              = ParseAgentType(dto.AgentType),
                Email             = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                ContactPerson     = dto.ContactPerson,
                WhatsApp          = dto.WhatsApp,
                Address           = dto.Address,
                CommissionPercent = dto.CommissionPercent,
                PaymentTerms      = dto.PaymentTerms,
                BankAccount       = dto.BankAccount,
                IFSC              = dto.IFSC,
                Notes             = dto.Notes,
                IsActive          = true,
                OrgId             = _tenant.OrgId > 0 ? _tenant.OrgId : 1,
            };
            await _context.TravelAgents.AddAsync(agent);
            await _context.SaveChangesAsync();
            return agent;
        }

        public async Task<TravelAgent?> UpdateAgent(int id, AddAgentDTO dto)
        {
            var agent = await _context.TravelAgents.FindAsync(id);
            if (agent == null || !_tenant.CanAccess(agent.OrgId)) return null;

            agent.Name              = dto.Name;
            agent.ContactNumber     = dto.ContactNumber;
            agent.type              = ParseAgentType(dto.AgentType);
            agent.Email             = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            agent.ContactPerson     = dto.ContactPerson;
            agent.WhatsApp          = dto.WhatsApp;
            agent.Address           = dto.Address;
            agent.CommissionPercent = dto.CommissionPercent;
            agent.PaymentTerms      = dto.PaymentTerms;
            agent.BankAccount       = dto.BankAccount;
            agent.IFSC              = dto.IFSC;
            agent.Notes             = dto.Notes;
            agent.IsActive          = dto.IsActive;

            await _context.SaveChangesAsync();
            return agent;
        }

        public async Task<decimal> ApplyAgentPayment(AddAgentPaymentDto dto)
        {
            if (dto.TotalPaidAmount <= 0)
                throw new ArgumentException("Payment amount must be greater than zero.");

            var agent = await GetByIdAsync(dto.AgentId)
                ?? throw new KeyNotFoundException("Travel agent not found");

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                // Oldest open allocation first. Matches Agent AND Owner allocations —
                // bookings for TravelOwner partners are created as PayerType.Owner and
                // were silently skipped, so their payments applied nothing.
                var open = await _context.BookingPaymentAllocations
                    .Include(a => a.Booking)
                    .Where(a => a.TravelAgentId == agent.AgentId
                             && (a.PayerType == PayerType.Agent || a.PayerType == PayerType.Owner)
                             && a.AllocatedAmount > a.PaidAmount
                             && a.Booking.Status != Status.Canceled
                             && (!_tenant.ShouldFilter || a.Booking.OrgId == _tenant.OrgId))
                    .OrderBy(a => a.Booking.travelDate).ThenBy(a => a.BookingId)
                    .ToListAsync();

                decimal remaining = dto.TotalPaidAmount;
                decimal applied   = 0;

                foreach (var allocation in open)
                {
                    if (remaining <= 0) break;
                    decimal toApply = Math.Min(remaining, allocation.AllocatedAmount - allocation.PaidAmount);

                    _context.Payments.Add(new Payments
                    {
                        AmountPaid    = toApply,
                        PaymentDate   = DateTime.Now,
                        PaymentMethod = "Cash",
                        BookingId     = allocation.BookingId,
                        TravelAgentId = agent.AgentId
                    });
                    allocation.PaidAmount += toApply;

                    remaining -= toApply;
                    applied   += toApply;
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return applied;
            });
        }

        public async Task<List<Booking>> GetAgentBookingsById(int agentId)
        {
            var q = _context.Bookings.Where(b => b.TravelAgentId == agentId && b.Status != Status.Canceled);
            if (_tenant.ShouldFilter)
                q = q.Where(b => b.OrgId == _tenant.OrgId);
            return await q.OrderByDescending(b => b.travelDate).ToListAsync();
        }

        public async Task<List<Booking>> GetAgentReportBookingsById(int agentId, DateOnly? fromDate, DateOnly? toDate)
        {
            var query = _context.Bookings
                .Include(b => b.TravelAgent)
                .Include(b => b.Vehicle)
                .Include(b => b.Customer)
                .Where(b => b.TravelAgentId == agentId);

            if (_tenant.ShouldFilter)
                query = query.Where(b => b.OrgId == _tenant.OrgId);

            if (fromDate.HasValue && toDate.HasValue)
                query = query.Where(b => b.travelDate >= fromDate.Value && b.travelDate <= toDate.Value);

            return await query.OrderByDescending(b => b.travelDate).ToListAsync();
        }
    }
}
