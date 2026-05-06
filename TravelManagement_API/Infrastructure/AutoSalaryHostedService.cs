using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Infrastructure
{
    public class AutoSalaryHostedService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AutoSalaryHostedService> _logger;
        private Timer? _timer;

        public AutoSalaryHostedService(IServiceProvider services, ILogger<AutoSalaryHostedService> logger)
        {
            _services  = services;
            _logger    = logger;
        }

        public Task StartAsync(CancellationToken ct)
        {
            // Fire immediately on start, then every 24 h
            var now           = DateTime.Now;
            var nextMidnight  = now.Date.AddDays(1);
            var delay         = nextMidnight - now;
            _timer = new Timer(RunSalaryCheck, null, delay, TimeSpan.FromHours(24));
            return Task.CompletedTask;
        }

        private async void RunSalaryCheck(object? state)
        {
            var today = DateTime.Today;
            _logger.LogInformation("AutoSalaryHostedService: running check for {Date}", today.ToString("yyyy-MM-dd"));

            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Drivers eligible for auto-salary today
                var drivers = await db.Users
                    .Where(u => u.IsSalaryActive && !u.IsDeleted && u.Status && u.Licence != null)
                    .ToListAsync();

                foreach (var driver in drivers)
                {
                    if (driver.SalaryDay != today.Day) continue;

                    // Skip if already created this month
                    if (driver.LastAutoSalaryDate.HasValue &&
                        driver.LastAutoSalaryDate.Value.Year  == today.Year &&
                        driver.LastAutoSalaryDate.Value.Month == today.Month)
                        continue;

                    var noteText = $"Auto: {driver.EmployeeName} salary for {today:MMMM yyyy}";

                    bool salaryExists = await db.salaries.AnyAsync(s =>
                        s.userID == driver.userId && s.Month == today.Month && s.Year == today.Year);

                    if (!salaryExists)
                    {
                        db.salaries.Add(new Salary
                        {
                            userID      = driver.userId,
                            Month       = today.Month,
                            Year        = today.Year,
                            BaseSalay   = driver.Salary,
                            Overtimepay = 0,
                            Deduction   = 0,
                            NetSalaey   = driver.Salary,
                            IsPaid      = false,
                            Notes       = noteText,
                        });
                    }

                    // Also create a VehicleExpence record so salary appears in Expenses page
                    bool expenseExists = await db.vehicleExpences.AnyAsync(e =>
                        e.CategoryType == Category.Salary &&
                        e.Notes == noteText &&
                        e.ExpenseDate.Month == today.Month &&
                        e.ExpenseDate.Year  == today.Year);

                    if (!expenseExists)
                    {
                        db.vehicleExpences.Add(new VehicleExpence
                        {
                            ExpenseDate  = today,
                            Amount       = driver.Salary,
                            CategoryType = Category.Salary,
                            VehicleID    = null,   // salary isn't tied to a specific vehicle
                            Notes        = noteText,
                        });
                    }

                    driver.LastAutoSalaryDate = today;
                }

                await db.SaveChangesAsync();
                _logger.LogInformation("AutoSalaryHostedService: processed {Count} drivers", drivers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoSalaryHostedService: error during salary check");
            }
        }

        public Task StopAsync(CancellationToken ct)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose() => _timer?.Dispose();
    }
}
