using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Entities
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Customers> Customers { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<ExternalEmployee> ExternalEmployees { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VehicleExpence> vehicleExpences { get; set; }
        public DbSet<Salary> salaries { get; set; }
        public DbSet<Documents> Documents { get; set; }
        public DbSet<OvertimeLog> overtimeLogs { get; set; }
        public DbSet<VehicleMaintenanceShedule> vehicleMaintenanceShedules { get; set; }
        public DbSet<TravelAgent> TravelAgents { get; set; }
        public DbSet<Payments> Payments { get; set; }
        public DbSet<BookingPaymentAllocation> BookingPaymentAllocations { get; set; }
        public DbSet<EmailInquiry> EmailInquiries { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ExternalEmployeeCashCollection> ExternalEmployeeCashCollections { get; set; }
        public DbSet<RateChart> RateCharts { get; set; }
        public DbSet<AgentCashCollection> AgentCashCollections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Decimal precision (provider-agnostic — works for both SQL Server and PostgreSQL)
            modelBuilder.Entity<Booking>().Property(b => b.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Salary>().Property(s => s.BaseSalay).HasPrecision(18, 2);
            modelBuilder.Entity<Salary>().Property(s => s.Deduction).HasPrecision(18, 2);
            modelBuilder.Entity<Salary>().Property(s => s.Overtimepay).HasPrecision(18, 2);
            modelBuilder.Entity<Salary>().Property(s => s.NetSalaey).HasPrecision(18, 2);
            modelBuilder.Entity<User>().Property(s => s.Salary).HasPrecision(18, 2);
            modelBuilder.Entity<VehicleExpence>().Property(a => a.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<BookingPaymentAllocation>().Property(a => a.AllocatedAmount).HasPrecision(18, 2);
            modelBuilder.Entity<BookingPaymentAllocation>().Property(a => a.PaidAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Payments>().Property(a => a.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<ExternalEmployeeCashCollection>().Property(a => a.CommissionAmount).HasPrecision(18, 2);
            modelBuilder.Entity<ExternalEmployeeCashCollection>().Property(a => a.BookingAmount).HasPrecision(18, 2);
            modelBuilder.Entity<ExternalEmployeeCashCollection>().Property(a => a.TotalPaidToVendor).HasPrecision(18, 2);
            modelBuilder.Entity<OvertimeLog>().Property(o => o.hours).HasPrecision(5, 2);
            modelBuilder.Entity<VehicleMaintenanceShedule>().Property(v => v.cost).HasPrecision(10, 2);
            modelBuilder.Entity<TravelAgent>().Property(t => t.CommissionRate).HasPrecision(18, 2);
            modelBuilder.Entity<TravelAgent>().Property(t => t.CommissionPercent).HasPrecision(18, 2);
            modelBuilder.Entity<Vehicle>().Property(v => v.EMIAmount).HasPrecision(18, 2);
            modelBuilder.Entity<AgentCashCollection>().Property(a => a.AmountCollected).HasPrecision(18, 2);

            // Unique indexes
            modelBuilder.Entity<User>().HasIndex(u => u.UserName).IsUnique();

            // Performance indexes
            modelBuilder.Entity<Booking>().HasIndex(b => b.travelDate);
            modelBuilder.Entity<Booking>().HasIndex(b => b.VehicleId);
            modelBuilder.Entity<Booking>().HasIndex(b => b.Userid);
            modelBuilder.Entity<VehicleExpence>().HasIndex(e => e.ExpenseDate);
            modelBuilder.Entity<VehicleExpence>().HasIndex(e => e.VehicleID);
            modelBuilder.Entity<Salary>().HasIndex(s => new { s.userID, s.Month, s.Year });

            base.OnModelCreating(modelBuilder);
        }
    }
}
