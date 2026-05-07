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
            var now          = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            _timer = new Timer(RunSalaryCheck, null, nextMidnight - now, TimeSpan.FromHours(24));
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

                // Only active, non-deleted drivers whose salary day matches today
                // and who have not yet received salary for this month
                var drivers = await db.Users
                    .Where(u => u.IsSalaryActive
                             && !u.IsDeleted
                             && u.Status
                             && u.Licence != null
                             && u.SalaryDay == today.Day
                             && (u.LastAutoSalaryDate == null
                                 || u.LastAutoSalaryDate.Value.Year  != today.Year
                                 || u.LastAutoSalaryDate.Value.Month != today.Month))
                    .ToListAsync();

                foreach (var driver in drivers)
                {
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
                            Notes       = $"Auto: {driver.EmployeeName} salary for {today:MMMM yyyy}",
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
