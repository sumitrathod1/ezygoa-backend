using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class RateChartRepository : IRateChartRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public RateChartRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        private IQueryable<RateChart> OrgRateCharts =>
            _tenant.ShouldFilter
                ? _context.RateCharts.Where(r => r.OrgId == _tenant.OrgId)
                : _context.RateCharts;

        public Task<List<RateChart>> GetAllAsync() =>
            OrgRateCharts.OrderByDescending(r => r.UpdatedAt).ToListAsync();

        public Task<RateChart?> GetByIdAsync(string id) =>
            OrgRateCharts.FirstOrDefaultAsync(r => r.Id == id);

        public async Task<RateChart> CreateAsync(RateChart chart)
        {
            if (string.IsNullOrEmpty(chart.Id))
                chart.Id = Guid.NewGuid().ToString();
            chart.CreatedAt = DateTime.UtcNow;
            chart.UpdatedAt = DateTime.UtcNow;
            chart.OrgId     = _tenant.OrgId > 0 ? _tenant.OrgId : 1;
            await _context.RateCharts.AddAsync(chart);
            await _context.SaveChangesAsync();
            return chart;
        }

        public async Task<RateChart?> UpdateAsync(string id, RateChart chart)
        {
            var existing = await _context.RateCharts.FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return null;

            existing.TemplateName    = chart.TemplateName;
            existing.AgentName       = chart.AgentName;
            existing.AgentNumber     = chart.AgentNumber;
            existing.CompanyName     = chart.CompanyName;
            existing.Tagline         = chart.Tagline;
            existing.ValidFrom       = chart.ValidFrom;
            existing.ValidTo         = chart.ValidTo;
            existing.SpecialDaysNote = chart.SpecialDaysNote;
            existing.Locations       = chart.Locations;
            existing.VehiclesJson    = chart.VehiclesJson;
            existing.RoutesJson      = chart.RoutesJson;
            existing.SurchargesJson  = chart.SurchargesJson;
            existing.NotesJson       = chart.NotesJson;
            existing.FooterJson      = chart.FooterJson;
            existing.Currency        = chart.Currency;
            existing.SeasonMode      = chart.SeasonMode;
            existing.PeakSeasonDates = chart.PeakSeasonDates;
            existing.UpdatedAt       = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var chart = await _context.RateCharts.FindAsync(id);
            if (chart == null) return false;
            _context.RateCharts.Remove(chart);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
