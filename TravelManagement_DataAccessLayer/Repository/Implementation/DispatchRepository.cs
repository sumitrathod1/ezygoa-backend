using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    /// <summary>
    /// Assignment board (which booking still needs a driver / vehicle), quick assign,
    /// and one-click hand-over of a booking to a linked organisation.
    /// </summary>
    public class DispatchRepository : IDispatchRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public DispatchRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        /// <summary>Today in India — the server may run on UTC, which is still "yesterday" until 5:30 am IST.</summary>
        private static DateOnly Today => AssignmentRules.TodayInIndia();

        /// <summary>Past bookings still missing a driver / vehicle are shown for this many days.</summary>
        private const int MissedDays = 14;

        private IQueryable<Booking> OrgBookings =>
            _tenant.ShouldFilter ? _context.Bookings.Where(b => b.OrgId == _tenant.OrgId) : _context.Bookings;

        /// <summary>Own-fleet bookings that are still open (partner bookings have their own driver).</summary>
        private IQueryable<Booking> OpenOwnFleet =>
            OrgBookings.Where(b => b.ExternalEmployeeId == null
                                && b.Status != Status.Canceled
                                && b.Status != Status.Completed);

        private static string T(TimeOnly? t) => t?.ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? "";
        private static string D(DateOnly d) => d.ToString("dd MMM", CultureInfo.InvariantCulture);

        // ── Board ───────────────────────────────────────────────────────────

        public async Task<object> GetBoardAsync(DateOnly from, DateOnly to)
        {
            if (to < from) throw new ArgumentException("'To' date cannot be before 'From' date.");
            if (to.DayNumber - from.DayNumber > 62) throw new ArgumentException("Choose at most two months at a time.");

            var orgId = _tenant.WriteOrgId;
            var today = Today;
            var missedFrom = today.AddDays(-MissedDays);

            // Everything that occupies a driver or car in the range — including self-drive
            // rentals that started earlier and are still out — plus recent past bookings
            // whose date has gone by without a driver / vehicle (they must not just vanish)
            var rows = await OrgBookings.AsNoTracking()
                .Where(b => b.ExternalEmployeeId == null && b.Status != Status.Canceled
                         && ((b.travelDate <= to && (b.ReturnDate ?? b.travelDate) >= from)
                             || (b.travelDate >= missedFrom && b.travelDate < today && b.Status != Status.Completed
                                 && (b.VehicleId == null || (b.Userid == null && b.BookingType != BookingType.SelfDriveRental)))))
                .OrderBy(b => b.travelDate).ThenBy(b => b.Traveltime)
                .Select(b => new
                {
                    b.BookingId, b.travelDate, b.ReturnDate, b.Traveltime, b.From, b.To, b.Pax,
                    b.BookingType, b.Status, b.Amount, b.VehicleId, b.Userid, b.TransferredFromOrgId, b.TripId, b.EndDrop, b.StartPickup, b.FullDayHours,
                    CustomerName   = b.Customer != null ? b.Customer.CustomerName : null,
                    CustomerNumber = b.Customer != null ? b.Customer.CustomerNumber : null,
                    VehicleName    = b.Vehicle != null ? b.Vehicle.VehicleName : null,
                    VehicleNumber  = b.Vehicle != null ? b.Vehicle.VehicleNumber : null,
                    DriverName     = b.user != null ? b.user.EmployeeName : null,
                })
                .ToListAsync();

            var fromOrgIds = rows.Where(r => r.TransferredFromOrgId != null).Select(r => r.TransferredFromOrgId!.Value).Distinct().ToList();
            var orgNames = await _context.Organizations.AsNoTracking()
                .Where(o => fromOrgIds.Contains(o.OrgId)).ToDictionaryAsync(o => o.OrgId, o => o.Name);

            // "Day 2 of 4" for trip bookings, counted over the whole trip (not just this range)
            var tripIds = rows.Where(r => r.TripId != null).Select(r => r.TripId!.Value).Distinct().ToList();
            var tripDays = (await _context.Bookings.AsNoTracking()
                    .Where(b => b.TripId != null && tripIds.Contains(b.TripId.Value) && b.Status != Status.Canceled)
                    .Select(b => new { TripId = b.TripId!.Value, b.BookingId, b.travelDate, b.Traveltime })
                    .ToListAsync())
                .GroupBy(x => x.TripId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.travelDate).ThenBy(x => x.Traveltime).Select(x => x.BookingId).ToList());

            var bookings = rows.Select(r =>
            {
                var days = r.TripId != null ? tripDays.GetValueOrDefault(r.TripId.Value) : null;
                var needsDriver = r.BookingType != BookingType.SelfDriveRental;
                var done = r.VehicleId != null && (r.Userid != null || !needsDriver);
                return new
                {
                    r.BookingId,
                    TravelDate = r.travelDate, r.ReturnDate,
                    TravelTime = r.Traveltime,
                    r.From, r.To, r.Pax, r.Amount,
                    BookingType = r.BookingType.ToString(),
                    Status = r.Status.ToString(),
                    r.CustomerName, r.CustomerNumber,
                    r.VehicleId, r.VehicleName, r.VehicleNumber,
                    UserId = r.Userid, r.DriverName,
                    NeedsDriver = needsDriver,
                    FullyAssigned = done,
                    IsOpen = r.Status != Status.Completed,
                    IsMissed = !done && r.Status != Status.Completed && r.travelDate < today,
                    TransferredFrom = r.TransferredFromOrgId != null ? orgNames.GetValueOrDefault(r.TransferredFromOrgId.Value) : null,
                    r.TripId,
                    r.EndDrop,
                    r.StartPickup,
                    r.FullDayHours,
                    TripDay  = days == null ? (int?)null : days.IndexOf(r.BookingId) + 1,
                    TripDays = days?.Count,
                };
            }).ToList();

            var drivers = await _context.Users.AsNoTracking()
                .Where(u => u.Role == Role.Employee && !u.IsDeleted && (!_tenant.ShouldFilter || u.OrgId == orgId))
                .OrderBy(u => u.EmployeeName)
                .Select(u => new { u.userId, Name = u.EmployeeName, u.Number })
                .ToListAsync();

            var vehicles = await _context.Vehicles.AsNoTracking()
                .Where(v => !_tenant.ShouldFilter || v.OrgId == orgId)
                .OrderBy(v => v.VehicleName)
                .Select(v => new { v.VehicleId, v.VehicleName, v.VehicleNumber, VehicleType = v.VehicleType.ToString(), v.Seatingcapacity })
                .ToListAsync();

            return new
            {
                from, to,
                bookings,
                drivers,
                vehicles = vehicles.Select(v => new
                {
                    v.VehicleId, v.VehicleName, v.VehicleNumber, v.VehicleType, v.Seatingcapacity,
                    RentalOnly = AssignmentRules.IsRentalPlaceholder(v.VehicleNumber),
                }),
            };
        }

        // ── Header badge ────────────────────────────────────────────────────

        public async Task<object> GetPendingAssignmentCountAsync()
        {
            var today = Today;
            var missedFrom = today.AddDays(-MissedDays);
            var all = await OpenOwnFleet
                .Where(b => b.travelDate >= missedFrom
                         && (b.VehicleId == null || (b.Userid == null && b.BookingType != BookingType.SelfDriveRental)))
                .Select(b => b.travelDate)
                .ToListAsync();
            var dates = all.Where(d => d >= today).ToList();

            var tomorrow = today.AddDays(1);
            return new
            {
                total    = dates.Count,
                // Date already passed, still no driver / vehicle (last 14 days)
                missed   = all.Count(d => d < today),
                today    = dates.Count(d => d == today),
                tomorrow = dates.Count(d => d == tomorrow),
                later    = dates.Count(d => d > tomorrow),
                nextDate = dates.Count > 0 ? dates.Min() : (DateOnly?)null,
            };
        }

        // ── Assign ──────────────────────────────────────────────────────────

        public async Task<AssignBookingResult> AssignAsync(int bookingId, AssignBookingDTO dto)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null || !_tenant.CanAccess(booking.OrgId))
                throw new KeyNotFoundException($"Booking {bookingId} not found");
            if (booking.Status == Status.Canceled) throw new ArgumentException("This booking is cancelled.");
            if (booking.Status == Status.Completed) throw new ArgumentException("This booking is already completed.");
            if (booking.ExternalEmployeeId != null)
                throw new ArgumentException("This booking is given to a partner. Open it in the booking form to bring it back to your own fleet.");

            Vehicle? vehicle = null;
            if (dto.ClearVehicle) booking.VehicleId = null;
            else if (dto.VehicleId is > 0)
            {
                vehicle = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId);
                if (vehicle == null || vehicle.OrgId != booking.OrgId)
                    throw new ArgumentException("The selected vehicle was not found.");
                if (AssignmentRules.IsRentalPlaceholder(vehicle.VehicleNumber) && booking.BookingType != BookingType.SelfDriveRental)
                    throw new ArgumentException($"{vehicle.VehicleName} is a rent-a-car listing. It can only be given to self-drive rentals.");
                booking.VehicleId = vehicle.VehicleId;
            }

            User? driver = null;
            if (dto.ClearDriver) booking.Userid = null;
            else if (dto.UserId is > 0)
            {
                driver = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.userId == dto.UserId);
                if (driver == null || driver.OrgId != booking.OrgId || driver.IsDeleted || driver.Role != Role.Employee)
                    throw new ArgumentException("The selected driver was not found.");
                booking.Userid = driver.userId;
            }

            AssignmentRules.SyncStatus(booking);
            await _context.SaveChangesAsync();

            vehicle ??= booking.VehicleId == null ? null : await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.VehicleId == booking.VehicleId);
            driver  ??= booking.Userid == null ? null : await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.userId == booking.Userid);

            return new AssignBookingResult
            {
                BookingId   = booking.BookingId,
                VehicleId   = booking.VehicleId,
                VehicleName = vehicle?.VehicleName,
                UserId      = booking.Userid,
                DriverName  = driver?.EmployeeName,
                Status      = booking.Status.ToString(),
                FullyAssigned = AssignmentRules.IsFullyAssigned(booking),
                Warnings    = await ClashWarningsAsync(booking, vehicle, driver,
                                  checkVehicle: dto.VehicleId is > 0 && !dto.ClearVehicle,
                                  checkDriver: dto.UserId is > 0 && !dto.ClearDriver),
                OrgId       = booking.OrgId,
            };
        }

        /// <summary>
        /// Other bookings the same driver / car already has on those days. A warning,
        /// not a block: one driver often does an airport drop and a pickup the same day.
        /// </summary>
        private async Task<List<string>> ClashWarningsAsync(Booking b, Vehicle? vehicle, User? driver, bool checkVehicle, bool checkDriver)
        {
            var warnings = new List<string>();
            var start = b.travelDate;
            var end   = b.ReturnDate ?? b.travelDate;

            async Task Check(string who, IQueryable<Booking> same)
            {
                var others = await same
                    .Where(o => o.BookingId != b.BookingId && o.Status != Status.Canceled
                             && o.travelDate <= end && (o.ReturnDate ?? o.travelDate) >= start)
                    .OrderBy(o => o.travelDate).ThenBy(o => o.Traveltime)
                    .Select(o => new { o.BookingId, o.travelDate, o.ReturnDate, o.Traveltime, o.From, o.To, o.BookingType })
                    .Take(5).ToListAsync();

                foreach (var o in others)
                {
                    var close = b.Traveltime.HasValue && o.Traveltime.HasValue && o.travelDate == b.travelDate
                             && Math.Abs((b.Traveltime.Value.ToTimeSpan() - o.Traveltime.Value.ToTimeSpan()).TotalHours) < 3;
                    var rental = o.BookingType == BookingType.SelfDriveRental && o.ReturnDate.HasValue
                        ? $"is out on rental #{o.BookingId} until {D(o.ReturnDate.Value)}"
                        : $"already has booking #{o.BookingId}{(o.Traveltime.HasValue ? " at " + T(o.Traveltime) : "")} on {D(o.travelDate)} ({o.From} → {o.To})";
                    warnings.Add((close ? "Time clash: " : "") + $"{who} {rental}.");
                }
            }

            if (checkVehicle && vehicle != null)
                await Check(vehicle.VehicleName ?? vehicle.VehicleNumber ?? "This vehicle",
                    _context.Bookings.Where(o => o.OrgId == b.OrgId && o.VehicleId == vehicle.VehicleId));
            if (checkDriver && driver != null)
                await Check(driver.EmployeeName,
                    _context.Bookings.Where(o => o.OrgId == b.OrgId && o.Userid == driver.userId));
            return warnings;
        }

        // ── Transfer between linked organisations ───────────────────────────

        private async Task<List<int>> LinkedOrgIdsAsync(int orgId) =>
            await _context.OrgLinks.AsNoTracking()
                .Where(l => l.OrgAId == orgId || l.OrgBId == orgId)
                .Select(l => l.OrgAId == orgId ? l.OrgBId : l.OrgAId)
                .ToListAsync();

        public async Task<List<object>> GetTransferTargetsAsync()
        {
            if (!_tenant.ShouldFilter) return new();
            var ids = await LinkedOrgIdsAsync(_tenant.OrgId);
            return await _context.Organizations.AsNoTracking()
                .Where(o => ids.Contains(o.OrgId) && o.IsActive)
                .OrderBy(o => o.Name)
                .Select(o => (object)new { o.OrgId, o.Name, o.Code })
                .ToListAsync();
        }

        public async Task<TransferBookingResult> TransferAsync(int bookingId, TransferBookingDTO dto)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await TransferCoreAsync(bookingId, dto);
                    await tx.CommitAsync();
                    return result;
                }
                catch
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }

        private async Task<TransferBookingResult> TransferCoreAsync(int bookingId, TransferBookingDTO dto)
        {
            var booking = await _context.Bookings.Include(b => b.Customer).FirstOrDefaultAsync(b => b.BookingId == bookingId);
            if (booking == null || !_tenant.CanAccess(booking.OrgId))
                throw new KeyNotFoundException($"Booking {bookingId} not found");

            var sourceOrgId = booking.OrgId;
            if (dto.TargetOrgId == sourceOrgId) throw new ArgumentException("The booking is already in this organisation.");
            if (booking.Status == Status.Canceled) throw new ArgumentException("A cancelled booking cannot be transferred.");
            if (booking.Status == Status.Completed) throw new ArgumentException("A completed booking cannot be transferred.");

            var target = await _context.Organizations.FirstOrDefaultAsync(o => o.OrgId == dto.TargetOrgId && o.IsActive);
            var linked = (await LinkedOrgIdsAsync(sourceOrgId)).Contains(dto.TargetOrgId);
            if (target == null || !linked)
                throw new ArgumentException("You can only transfer to a linked organisation. Ask the super admin to link them first.");
            var source = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrgId == sourceOrgId);

            // Money already moved with a partner or agent belongs to this organisation's books —
            // moving the booking would leave those records pointing at the wrong company.
            if (await _context.Payments.AnyAsync(p => p.BookingId == bookingId && p.AmountPaid != 0))
                throw new ArgumentException("An agent payment is recorded on this booking, so it cannot be transferred.");
            var agentAllocs = await _context.BookingPaymentAllocations
                .Where(a => a.BookingId == bookingId && a.PayerType != PayerType.Customer).ToListAsync();
            if (agentAllocs.Any(a => a.PaidAmount != 0))
                throw new ArgumentException("The agent / owner has already paid for this booking, so it cannot be transferred.");

            var settlement = await _context.ExternalEmployeeCashCollections.FirstOrDefaultAsync(s => s.BookingId == bookingId);
            if (settlement != null &&
                (settlement.TotalPaidToVendor != 0 || await _context.SettlementPayments.AnyAsync(p => p.BookingId == bookingId)))
                throw new ArgumentException("Money is already recorded with the partner on this booking, so it cannot be transferred.");

            // Customer: same person in the other organisation, matched by phone, or a copy
            var oldCustomer = booking.Customer;
            Customers? customer = null;
            if (oldCustomer != null)
            {
                var variants = BookingRepository.PhoneVariants(oldCustomer.CustomerNumber);
                customer = await _context.Customers
                    .Where(c => c.OrgId == target.OrgId && c.CustomerNumber != null && variants.Contains(c.CustomerNumber))
                    .OrderBy(c => c.CustomersId).FirstOrDefaultAsync();
                if (customer == null)
                {
                    customer = new Customers
                    {
                        CustomerName    = oldCustomer.CustomerName,
                        CustomerNumber  = oldCustomer.CustomerNumber,
                        AlternateNumber = oldCustomer.AlternateNumber,
                        TravelDate      = booking.travelDate,
                        OrgId           = target.OrgId,
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                }
            }

            // Partner booking: the partner moves too (matched by phone in the other organisation)
            if (booking.ExternalEmployeeId != null)
            {
                var oldPartner = await _context.ExternalEmployees.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.externalEmployeeID == booking.ExternalEmployeeId);
                var variants = BookingRepository.PhoneVariants(oldPartner?.externalEmployeeNumber);
                var partner = await _context.ExternalEmployees
                    .Where(e => e.OrgId == target.OrgId && e.externalEmployeeNumber != null && variants.Contains(e.externalEmployeeNumber))
                    .OrderBy(e => e.externalEmployeeID).FirstOrDefaultAsync();
                if (partner == null)
                {
                    partner = new ExternalEmployee
                    {
                        externalEmployeeName   = oldPartner?.externalEmployeeName,
                        externalEmployeeNumber = oldPartner?.externalEmployeeNumber,
                        OrgId                  = target.OrgId,
                    };
                    _context.ExternalEmployees.Add(partner);
                    await _context.SaveChangesAsync();
                }
                booking.ExternalEmployeeId = partner.externalEmployeeID;
                if (settlement != null) settlement.ExternalEmployeeId = partner.externalEmployeeID;
            }

            // Agents, drivers and cars belong to the old organisation
            _context.BookingPaymentAllocations.RemoveRange(agentAllocs);
            booking.TravelAgentId = null;
            booking.VehicleId = null;
            booking.Userid = null;
            // The trip (hotel, other days) stays with the old organisation
            var oldTripId = booking.TripId;
            booking.TripId = null;

            // The customer's advance stays with the booking so the driver collects the right balance
            var custAlloc = await _context.BookingPaymentAllocations
                .Where(a => a.BookingId == bookingId && a.PayerType == PayerType.Customer).ToListAsync();
            foreach (var a in custAlloc) a.CustomerId = customer?.CustomersId;
            if (customer != null) booking.CustomerID = customer.CustomersId;

            // Follow-up messages and review requests go with the booking
            foreach (var f in await _context.FollowUps.Where(f => f.BookingId == bookingId).ToListAsync()) f.OrgId = target.OrgId;
            foreach (var r in await _context.Reviews.Where(r => r.BookingId == bookingId).ToListAsync()) r.OrgId = target.OrgId;

            booking.OrgId = target.OrgId;
            booking.TransferredFromOrgId = sourceOrgId;
            booking.TransferredAt = DateTime.UtcNow;
            AssignmentRules.SyncStatus(booking);

            var summary = $"Booking #{booking.BookingId} ({booking.From} -> {booking.To}, {booking.travelDate:dd MMM yyyy}) transferred from {source?.Name} to {target.Name}";

            // The automatic audit row lands in the new organisation; the old one keeps its own record
            _context.AuditLogs.Add(new AuditLog
            {
                OrgId      = sourceOrgId,
                UserId     = _tenant.UserId > 0 ? _tenant.UserId : null,
                UserName   = _tenant.UserName ?? (_tenant.UserId > 0 ? $"#{_tenant.UserId}" : "system"),
                Action     = "Transferred",
                EntityType = "Booking",
                EntityId   = booking.BookingId.ToString(),
                BookingId  = booking.BookingId,
                Summary    = summary,
                CreatedAt  = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();
            await BookingRepository.SyncTripDatesAsync(_context, oldTripId);

            return new TransferBookingResult
            {
                BookingId   = booking.BookingId,
                FromOrgId   = sourceOrgId,
                FromOrgName = source?.Name ?? "",
                ToOrgId     = target.OrgId,
                ToOrgName   = target.Name,
                Summary     = summary,
            };
        }
    }
}
