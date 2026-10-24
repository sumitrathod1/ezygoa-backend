using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class SalaryRepository : ISalaryRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public SalaryRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        private IQueryable<Salary> OrgSalaries =>
            _tenant.ShouldFilter
                ? _context.salaries.Where(s => s.OrgId == _tenant.OrgId)
                : _context.salaries;

        public async Task<List<Salary>> GetAllAsync()
        {
            return await OrgSalaries
                .Include(s => s.user)
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .ToListAsync();
        }

        public async Task<List<Salary>> GetByUserAsync(int userId)
        {
            return await OrgSalaries
                .Where(s => s.userID == userId)
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
                .ToListAsync();
        }

        public async Task<Salary?> GetByIdAsync(int id)
        {
            var salary = await _context.salaries.Include(s => s.user).FirstOrDefaultAsync(s => s.SalaryId == id);
            return salary != null && _tenant.CanAccess(salary.OrgId) ? salary : null;
        }

        public async Task<Salary> CreateAsync(Salary salary)
        {
            // The salary always belongs to the employee's org. An org admin may only
            // create salaries for their own employees (the body could carry any OrgId);
            // background jobs (no tenant) use the employee's org.
            var owner = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.userId == salary.userID);
            if (owner == null || !_tenant.CanAccess(owner.OrgId))
                throw new KeyNotFoundException("Employee not found");
            salary.OrgId = owner.OrgId;
            await _context.salaries.AddAsync(salary);
            await _context.SaveChangesAsync();
            return salary;
        }

        public async Task<Salary?> MarkPaidAsync(int id, DateTime paidDate, string? notes)
        {
            var salary = await _context.salaries.FindAsync(id);
            if (salary == null || !_tenant.CanAccess(salary.OrgId)) return null;
            salary.IsPaid   = true;
            salary.PaidDate = paidDate;
            if (notes != null) salary.Notes = notes;
            await _context.SaveChangesAsync();
            return salary;
        }

        public async Task<bool> ExistsAsync(int userId, int month, int year)
        {
            return await _context.salaries.AnyAsync(s => s.userID == userId && s.Month == month && s.Year == year);
        }

        public async Task<List<Salary>> GenerateMonthAsync(int month, int year)
        {
            // Only generate for employees in the current org
            var employeeQuery = (_tenant.ShouldFilter
                ? _context.Users.Where(u => u.OrgId == _tenant.OrgId)
                : _context.Users.AsQueryable())
                .Where(u => u.IsSalaryActive && !u.IsDeleted && u.Role == Role.Employee && u.Status);

            var employees = await employeeQuery.ToListAsync();

            var created = new List<Salary>();
            foreach (var emp in employees)
            {
                if (await ExistsAsync(emp.userId, month, year)) continue;
                var record = new Salary
                {
                    userID      = emp.userId,
                    Month       = month,
                    Year        = year,
                    BaseSalay   = emp.Salary,
                    Deduction   = 0,
                    Overtimepay = 0,
                    NetSalaey   = emp.Salary,
                    IsPaid      = false,
                    OrgId       = emp.OrgId,
                };
                await _context.salaries.AddAsync(record);
                created.Add(record);
            }
            await _context.SaveChangesAsync();
            return created;
        }
    }
}
