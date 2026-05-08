using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;

namespace TravelManagement.API.Infrastructure
{
    public class AutoEMIHostedService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AutoEMIHostedService> _logger;
        private Timer? _timer;

        public AutoEMIHostedService(IServiceProvider services, ILogger<AutoEMIHostedService> logger)
        {
            _services = services;
            _logger   = logger;
        }

        public Task StartAsync(CancellationToken ct)
        {
            var now          = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            // Run at the same time as AutoSalaryHostedService (next midnight, then every 24 h)
            _timer = new Timer(RunEMICheck, null, nextMidnight - now, TimeSpan.FromHours(24));
            return Task.CompletedTask;
        }

        private async void RunEMICheck(object? state)
        {
            var today = DateTime.Today;
            _logger.LogInformation("AutoEMIHostedService: running check for {Date}", today.ToString("yyyy-MM-dd"));

            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Vehicles with active EMI whose EMIDay matches today
                var vehicles = await db.Vehicles
                    .Where(v => v.HasEMI
                             && v.EMIAmount > 0
                             && v.EMIDay == today.Day
                             && (v.EMIEndDate == null || v.EMIEndDate.Value >= DateOnly.FromDateTime(today))
                             && (v.TotalEMIs == 0 || v.PaidEMIs < v.TotalEMIs))
                    .ToListAsync();

                foreach (var vehicle in vehicles)
                {
                    // Guard: skip if an EMI expense already exists for this vehicle this month
                    bool alreadyExists = await db.vehicleExpences.AnyAsync(e =>
                        e.VehicleID        == vehicle.VehicleId
                        && e.CategoryType  == Category.EMI
                        && e.ExpenseDate.Month == today.Month
                        && e.ExpenseDate.Year  == today.Year);

                    if (alreadyExists) continue;

                    db.vehicleExpences.Add(new VehicleExpence
                    {
                        VehicleID    = vehicle.VehicleId,
                        CategoryType = Category.EMI,
                        Amount       = vehicle.EMIAmount,
                        ExpenseDate  = today,
                        Notes        = $"Auto EMI: {vehicle.VehicleName} ({vehicle.EMILender ?? "lender"}) — instalment {vehicle.PaidEMIs + 1}" +
                                       (vehicle.TotalEMIs > 0 ? $"/{vehicle.TotalEMIs}" : ""),
                    });

                    vehicle.PaidEMIs += 1;
                }

                await db.SaveChangesAsync();
                _logger.LogInformation("AutoEMIHostedService: processed {Count} vehicles", vehicles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoEMIHostedService: error during EMI check");
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
