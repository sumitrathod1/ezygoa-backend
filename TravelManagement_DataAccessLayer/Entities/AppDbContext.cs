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
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<FollowUp> FollowUps { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<MarketingCampaign> MarketingCampaigns { get; set; }
        public DbSet<Place> Places { get; set; }
        public DbSet<RouteRate> RouteRates { get; set; }
        public DbSet<RentalRate> RentalRates { get; set; }
        public DbSet<InstagramQueue> InstagramQueues { get; set; }

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
            modelBuilder.Entity<EmailInquiry>().Property(e => e.QuotedAmount).HasPrecision(18, 2);
            modelBuilder.Entity<MarketingCampaign>().Property(m => m.Budget).HasPrecision(18, 2);
            modelBuilder.Entity<MarketingCampaign>().Property(m => m.Spent).HasPrecision(18, 2);
            modelBuilder.Entity<RouteRate>().Property(r => r.Price).HasPrecision(18, 2);
            modelBuilder.Entity<RouteRate>().Property(r => r.PeakPrice).HasPrecision(18, 2);
            modelBuilder.Entity<RouteRate>().Property(r => r.FloorPrice).HasPrecision(18, 2);
            modelBuilder.Entity<RentalRate>().Property(r => r.PricePerDay).HasPrecision(18, 2);
            modelBuilder.Entity<RentalRate>().Property(r => r.WeeklyRate).HasPrecision(18, 2);
            modelBuilder.Entity<RentalRate>().Property(r => r.SecurityDeposit).HasPrecision(18, 2);
            modelBuilder.Entity<RentalRate>().Property(r => r.ExtraKmRate).HasPrecision(18, 2);

            // Booking → User: two FK relationships to the same table — must be explicit
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.user)
                .WithMany()
                .HasForeignKey(b => b.Userid)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.CreatedBy)
                .WithMany()
                .HasForeignKey(b => b.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Unique indexes
            // UserName must be unique WITHIN an org (multi-tenancy)
            modelBuilder.Entity<User>().HasIndex(u => new { u.UserName, u.OrgId }).IsUnique();

            // Performance indexes
            modelBuilder.Entity<Booking>().HasIndex(b => b.travelDate);
            modelBuilder.Entity<Booking>().HasIndex(b => b.VehicleId);
            modelBuilder.Entity<Booking>().HasIndex(b => b.Userid);
            modelBuilder.Entity<VehicleExpence>().HasIndex(e => e.ExpenseDate);
            modelBuilder.Entity<VehicleExpence>().HasIndex(e => e.VehicleID);
            modelBuilder.Entity<Salary>().HasIndex(s => new { s.userID, s.Month, s.Year });

            // RouteRate: unique per route + vehicle type + org
            modelBuilder.Entity<RouteRate>()
                .HasIndex(r => new { r.FromPlaceId, r.ToPlaceId, r.VehicleType, r.OrgId })
                .IsUnique();

            // RentalRate: one rate per vehicle per org
            modelBuilder.Entity<RentalRate>()
                .HasIndex(r => new { r.VehicleId, r.OrgId })
                .IsUnique();

            // InstagramQueue: scheduler polls WHERE Status='pending' ORDER BY QueueId
            modelBuilder.Entity<InstagramQueue>()
                .HasIndex(q => new { q.Status, q.QueueId });

            modelBuilder.Entity<RentalRate>()
                .HasOne(r => r.Vehicle)
                .WithMany()
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            // RouteRate FKs
            modelBuilder.Entity<RouteRate>()
                .HasOne(r => r.FromPlace)
                .WithMany()
                .HasForeignKey(r => r.FromPlaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RouteRate>()
                .HasOne(r => r.ToPlace)
                .WithMany()
                .HasForeignKey(r => r.ToPlaceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Places seed data
            var southMsg = "We can drop you here but pickup from this area is outside our zone. Nearest pickup points: Madgaon Railway Station or Dabolim Airport.";

            modelBuilder.Entity<Place>().HasData(
                // ── North zone ──────────────────────────────────────────────────
                new Place { PlaceId =  1, Name = "Vagator",    Aliases = "Vagator Beach",                                           Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  2, Name = "Chapora",    Aliases = "Chapora Fort",                                            Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  3, Name = "Anjuna",     Aliases = "Anjuna Beach,Anjuna Flea Market",                         Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  4, Name = "Baga",       Aliases = "Baga Beach",                                              Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  5, Name = "Calangute",  Aliases = "Calangute Beach",                                         Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  6, Name = "Candolim",   Aliases = "Candolim Beach",                                          Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  7, Name = "Sinquerim",  Aliases = "Sinquerim Beach,Aguada",                                  Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  8, Name = "Arpora",     Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId =  9, Name = "Saligao",    Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 10, Name = "Parra",      Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 11, Name = "Mapusa",     Aliases = "Mapusa Market",                                           Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 12, Name = "Guirim",     Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 13, Name = "Porvorim",   Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 14, Name = "Nerul",      Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 15, Name = "Panjim",     Aliases = "Panaji,Panaji City,Panjim City,Goa Capital",              Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 16, Name = "Miramar",    Aliases = "Miramar Beach",                                           Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 17, Name = "Dona Paula", Aliases = "Dona Paula Beach,Dona Paula Jetty",                       Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 18, Name = "Taleigao",   Aliases = null,                                                      Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },
                new Place { PlaceId = 19, Name = "Bambolim",   Aliases = "Bambolim Beach,Goa University",                           Zone = PlaceZone.North, PickupAllowed = true,  DropAllowed = true,  OrgId = 1 },

                // ── Hubs ────────────────────────────────────────────────────────
                new Place { PlaceId = 20, Name = "Mopa Airport",     Aliases = "GOX,Mopa,Manohar International Airport,North Goa Airport",    Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },
                new Place { PlaceId = 21, Name = "Dabolim Airport",  Aliases = "GOI,Dabolim,Goa International Airport,Vasco Airport",         Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },
                new Place { PlaceId = 22, Name = "Thivim Railway",   Aliases = "Thivim Station,Thivim Railway Station",                       Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },
                new Place { PlaceId = 23, Name = "Karmali Railway",  Aliases = "Karmali Station,Old Goa Station,Karmali",                     Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },
                new Place { PlaceId = 24, Name = "Madgaon Railway",  Aliases = "Margao Railway,Margao Station,Madgaon Station,Margao",         Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },
                new Place { PlaceId = 25, Name = "Vasco Railway",    Aliases = "Vasco da Gama Railway,Vasco Station,Vasco",                   Zone = PlaceZone.Hub, PickupAllowed = true, DropAllowed = true, OrgId = 1 },

                // ── South drop-only ─────────────────────────────────────────────
                new Place { PlaceId = 26, Name = "Colva",    Aliases = "Colva Beach",    Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 },
                new Place { PlaceId = 27, Name = "Majorda",  Aliases = "Majorda Beach",  Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 },
                new Place { PlaceId = 28, Name = "Benaulim", Aliases = "Benaulim Beach", Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 },
                new Place { PlaceId = 29, Name = "Varca",    Aliases = "Varca Beach",    Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 },
                new Place { PlaceId = 30, Name = "Palolem",  Aliases = "Palolem Beach",  Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 },
                new Place { PlaceId = 31, Name = "Agonda",   Aliases = "Agonda Beach",   Zone = PlaceZone.South, PickupAllowed = false, DropAllowed = true, RestrictionMessage = southMsg, OrgId = 1 }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
