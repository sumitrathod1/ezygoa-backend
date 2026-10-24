using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class BookingRepository : IBookingRepository
    {
        private readonly AppDbContext _context;
        private readonly TenantContext _tenant;

        public BookingRepository(AppDbContext context, TenantContext tenant)
        {
            _context = context;
            _tenant  = tenant;
        }

        private IQueryable<Booking> OrgBookings =>
            _tenant.ShouldFilter
                ? _context.Bookings.Where(b => b.OrgId == _tenant.OrgId)
                : _context.Bookings;

        private IQueryable<BookingPaymentAllocation> OrgAllocations =>
            _tenant.ShouldFilter
                ? _context.BookingPaymentAllocations.Where(p => p.Booking.OrgId == _tenant.OrgId)
                : _context.BookingPaymentAllocations;

        private IQueryable<ExternalEmployeeCashCollection> OrgSettlements =>
            _tenant.ShouldFilter
                ? _context.ExternalEmployeeCashCollections.Where(s => s.Booking.OrgId == _tenant.OrgId)
                : _context.ExternalEmployeeCashCollections;

        /// <summary>Booking by ID, but only if it belongs to the caller's org.</summary>
        private async Task<Booking?> FindOrgBookingAsync(int bookingId)
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
            return booking != null && _tenant.CanAccess(booking.OrgId) ? booking : null;
        }

        public async Task<Booking?> GetByIdAsync(int id) => await FindOrgBookingAsync(id);

        public async Task<List<ExternalEmployee>> GetExternalEmployeesAsync()
        {
            return _tenant.ShouldFilter
                ? await _context.ExternalEmployees.Where(e => e.OrgId == _tenant.OrgId).ToListAsync()
                : await _context.ExternalEmployees.ToListAsync();
        }

        // ── Settlement rules: see SettlementMath (shared with statements and dashboard) ──

        private static decimal NetPosition(ExternalEmployeeCashCollection s, decimal advance) =>
            SettlementMath.NetPosition(s, advance);

        private static decimal SettledSoFar(ExternalEmployeeCashCollection s, decimal due) =>
            SettlementMath.SettledSoFar(s, due);

        private async Task<decimal> CustomerAdvanceAsync(int bookingId) =>
            await _context.BookingPaymentAllocations
                .Where(p => p.BookingId == bookingId && p.PayerType == PayerType.Customer)
                .SumAsync(p => (decimal?)p.PaidAmount) ?? 0;

        /// <summary>Earned revenue for ZipGoa/EzyGoa: full fare for own bookings, commission only for partner bookings.</summary>
        private static decimal EarnedAmount(Booking b, IReadOnlyDictionary<int, decimal> commissionByBooking) =>
            b.ExternalEmployeeId == null
                ? b.Amount
                : commissionByBooking.GetValueOrDefault(b.BookingId, 0);

        private async Task<Dictionary<int, decimal>> CommissionsAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds.ToList();
            return await _context.ExternalEmployeeCashCollections
                .Where(s => ids.Contains(s.BookingId))
                .GroupBy(s => s.BookingId)
                .Select(g => new { g.Key, Commission = g.Max(x => x.CommissionAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Commission);
        }

        public async Task<object> GetAllBookingsWithStatsAsync()
        {
            var bookings = await OrgBookings
                .AsNoTracking()
                .Include(b => b.user)
                .Include(b => b.CreatedBy)
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Include(b => b.ExternalEmployee)
                .Where(b => b.Status != Status.Canceled)
                .OrderByDescending(b => b.travelDate)
                .ToListAsync();

            // Only this org's allocations (previously every org's rows were loaded)
            var payments = await OrgAllocations
                .GroupBy(p => p.BookingId)
                .Select(g => new
                {
                    BookingId      = g.Key,
                    TotalAllocated = g.Sum(x => x.AllocatedAmount),
                    TotalPaid      = g.Where(x => x.PayerType == PayerType.Customer).Sum(x => x.PaidAmount),
                    CustomerWillPay = g.Where(x => x.PayerType == PayerType.Customer).Sum(x => x.AllocatedAmount),
                    OwnerWillPay   = g.Where(x => x.PayerType != PayerType.Customer).Sum(x => x.AllocatedAmount),
                })
                .ToListAsync();

            var paymentDict = payments.ToDictionary(p => p.BookingId, p => p);
            var commissions = await CommissionsAsync(bookings.Where(b => b.ExternalEmployeeId != null).Select(b => b.BookingId));

            var bookingsWithPayment = bookings.Select(b =>
            {
                paymentDict.TryGetValue(b.BookingId, out var pay);
                return new
                {
                    b.BookingId, b.travelDate, b.From, b.To, b.VehicleId, b.Vehicle,
                    b.Userid, b.user, b.BookingType, b.Traveltime, b.Status, b.Amount,
                    b.Pax, b.Assigned, b.CustomerID, b.Customer, b.ExternalEmployeeId,
                    b.ExternalEmployee, b.Payment, b.TravelAgentId, b.TravelAgent,
                    b.isValidAssignment, b.ReturnDate,
                    b.CreatedByUserId,
                    CreatedByName   = b.CreatedBy != null ? b.CreatedBy.EmployeeName : null,
                    AdvancePaid     = pay?.TotalPaid ?? 0,
                    TotalAllocated  = pay?.TotalAllocated ?? 0,
                    Balance         = b.Amount - (pay?.TotalPaid ?? 0),
                    CustomerWillPay = pay?.CustomerWillPay ?? 0,
                    OwnerWillPay    = pay?.OwnerWillPay ?? 0,
                    CommissionAmount = b.ExternalEmployeeId != null ? commissions.GetValueOrDefault(b.BookingId, 0) : (decimal?)null,
                    EarnedAmount    = EarnedAmount(b, commissions),
                };
            }).ToList();

            var today        = DateOnly.FromDateTime(DateTime.Today);
            var currentMonth = today.Month;
            var currentYear  = today.Year;

            var dayOfWeek       = (int)today.DayOfWeek;
            var daysSinceMonday = (dayOfWeek == 0) ? 6 : dayOfWeek - 1;
            var weekStart       = today.AddDays(-daysSinceMonday);
            var weekEnd         = weekStart.AddDays(6);

            // Revenue = what the business actually earns (commission only on partner
            // bookings). Gross booking value is reported separately.
            decimal Earned(IEnumerable<Booking> src) => src.Sum(b => EarnedAmount(b, commissions));

            var totalToday = Earned(bookings.Where(b => b.travelDate == today));
            // Week no longer stops at the month boundary (a week spanning two months was undercounted)
            var totalWeek  = Earned(bookings.Where(b => b.travelDate >= weekStart && b.travelDate <= weekEnd));
            var totalMonth = Earned(bookings.Where(b => b.travelDate.Month == currentMonth && b.travelDate.Year == currentYear));
            var totalYear  = Earned(bookings.Where(b => b.travelDate.Year == currentYear));
            var totalRevenue = Earned(bookings);

            // Averages over the part of the year that has actually elapsed — dividing by
            // 365 / 52 / 12 made every average look far too low until December.
            var daysElapsed  = Math.Max(1, today.DayOfYear);
            var weeksElapsed = Math.Max(1m, daysElapsed / 7m);

            return new
            {
                bookings = bookingsWithPayment,
                revenueStats = new
                {
                    today = totalToday,
                    week  = totalWeek,
                    month = totalMonth,
                    year  = totalYear,
                    total = totalRevenue
                },
                grossBookingValue = new
                {
                    month = bookings.Where(b => b.travelDate.Month == currentMonth && b.travelDate.Year == currentYear).Sum(b => b.Amount),
                    year  = bookings.Where(b => b.travelDate.Year == currentYear).Sum(b => b.Amount),
                    total = bookings.Sum(b => b.Amount),
                },
                averageStats = new
                {
                    dailyAvg   = Math.Round(totalYear / daysElapsed, 2),
                    weeklyAvg  = Math.Round(totalYear / weeksElapsed, 2),
                    monthlyAvg = Math.Round(totalYear / currentMonth, 2),
                    yearlyAvg  = totalYear
                }
            };
        }

        public async Task<bool> CancelBookingAsync(int bookingId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var booking = await FindOrgBookingAsync(bookingId);
                    if (booking == null) return false;

                    booking.Status = Status.Canceled;
                    booking.Amount = 0;

                    // Recorded payments are history (money actually received) — they are
                    // no longer zeroed. Only what is still owed is cleared.
                    var allocations = await _context.BookingPaymentAllocations
                        .Where(a => a.BookingId == bookingId).ToListAsync();
                    foreach (var allocation in allocations)
                    {
                        allocation.AllocatedAmount = 0;
                        allocation.PaidAmount = 0;
                    }

                    // A cancelled partner booking must stop showing as receivable/payable.
                    // Untouched settlements are removed; ones where money already moved are
                    // kept as history (the vendor list marks them Cancelled).
                    var settlements = await _context.ExternalEmployeeCashCollections
                        .Where(s => s.BookingId == bookingId).ToListAsync();
                    foreach (var s in settlements.Where(s => s.SettledAt == null && s.TotalPaidToVendor == 0))
                        _context.ExternalEmployeeCashCollections.Remove(s);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine("CancelBooking Error: " + ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Create or edit a booking. Runs in ONE transaction: customer, partner, booking,
        /// allocations and settlement are saved together or not at all (a failure used to
        /// leave a half-created customer / partner behind).
        /// </summary>
        public async Task<Booking> CreateBooking(NewBookiingDTO dto)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    var booking = await CreateBookingCore(dto);
                    await tx.CommitAsync();
                    return booking;
                }
                catch
                {
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    throw;
                }
            });
        }

        private async Task<Booking> CreateBookingCore(NewBookiingDTO dto)
        {
            int? nullVehicleId = dto.VehicleId > 0 ? dto.VehicleId : null;
            int? nullUserId = dto.UserId > 0 ? dto.UserId : null;
            bool isExternalBooking = !string.IsNullOrWhiteSpace(dto.ExternalEmployeeNumber);

            if (dto.Amount < 0)
                throw new ArgumentException("Booking amount cannot be negative.");
            if (isExternalBooking && ((dto.commissionAmount ?? 0) < 0 || (dto.commissionAmount ?? 0) > dto.Amount))
                throw new ArgumentException("Commission must be between 0 and the booking amount.");
            if ((dto.AdvancePay ?? 0) < 0)
                throw new ArgumentException("Advance cannot be negative.");
            if ((dto.AdvancePay ?? 0) > (decimal)dto.Amount && dto.Amount > 0)
                throw new ArgumentException("Advance cannot be more than the booking amount.");
            if ((dto.CustomerWillPay ?? 0) < 0 || (dto.OwnerWillPay ?? 0) < 0)
                throw new ArgumentException("Amounts cannot be negative.");
            if (dto.Pax < 0)
                throw new ArgumentException("Passengers cannot be negative.");
            if (string.IsNullOrWhiteSpace(dto.CustomerNumber) || DigitsOf(dto.CustomerNumber).Length < 10)
                throw new ArgumentException("Enter a valid customer phone number.");
            if (isExternalBooking && DigitsOf(dto.ExternalEmployeeNumber).Length < 10)
                throw new ArgumentException("Enter a valid partner phone number.");
            if (isExternalBooking && string.IsNullOrWhiteSpace(dto.ExternalEmployee))
                throw new ArgumentException("Enter the partner / driver name.");

            bool isPackage = dto.DayWiseBookings != null && dto.DayWiseBookings.Count > 1;
            if (isPackage)
            {
                if (dto.BookingId is > 0)
                    throw new ArgumentException("A saved package is stored as one booking per day — edit the individual day instead.");
                if (dto.DayWiseBookings!.Any(d => d.TravelDate == default))
                    throw new ArgumentException("Every package day needs a date.");
                if (dto.DayWiseBookings!.Any(d => d.Amount < 0))
                    throw new ArgumentException("Package day amounts cannot be negative.");
            }
            else
            {
                if (dto.BookingDate == default)
                    throw new ArgumentException("Travel date is required.");
                if (dto.ReturnDate.HasValue && dto.ReturnDate.Value < dto.BookingDate)
                    throw new ArgumentException("Return date cannot be before the travel date.");
            }

            // Vehicle and driver must exist and belong to this organisation (an unknown ID
            // used to fail as a foreign-key 500, another org's ID was silently accepted)
            if (!isExternalBooking && nullVehicleId.HasValue)
            {
                var v = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.VehicleId == nullVehicleId.Value);
                if (v == null || !_tenant.CanAccess(v.OrgId))
                    throw new ArgumentException("The selected vehicle was not found.");
            }
            if (!isExternalBooking && nullUserId.HasValue)
            {
                var u = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.userId == nullUserId.Value);
                if (u == null || !_tenant.CanAccess(u.OrgId) || u.IsDeleted)
                    throw new ArgumentException("The selected driver was not found.");
            }

            BookingType bookingType = Enum.TryParse<BookingType>(dto.BookingType, out var bType)
                ? bType : BookingType.Notspecified;
            Payment paymentSource = dto.Payment == "ExternalEmployee"
                ? Payment.ExternalEmployee : Payment.Admin;

            Booking? existingBooking = null;
            if (dto.BookingId is > 0)
            {
                existingBooking = await FindOrgBookingAsync(dto.BookingId.Value)
                    ?? throw new KeyNotFoundException($"Booking {dto.BookingId} not found");
            }

            // Status: only change it when the client actually sends one; editing a booking
            // must not silently reset Completed/Assigned back to Pending.
            Status bookingStatus = Enum.TryParse<Status>(dto.BookingStatus, out var status)
                ? status
                : existingBooking?.Status ?? Status.Pending;

            TravelAgent? agent = null;
            if (dto.TravelAgentId is > 0)
            {
                agent = await _context.TravelAgents.FirstOrDefaultAsync(x => x.AgentId == dto.TravelAgentId);
                if (agent != null && !_tenant.CanAccess(agent.OrgId)) agent = null;
            }

            var customer = await UpdateCustomer(dto);

            if (isPackage)
            {
                List<Booking> createdBookings = new();
                foreach (var dayBooking in dto.DayWiseBookings)
                {
                    BookingType dayBType = Enum.TryParse<BookingType>(dayBooking.BookingType, out var bt)
                        ? bt : BookingType.Notspecified;

                    var booking = new Booking
                    {
                        From          = dayBooking.From,
                        To            = dayBooking.To,
                        VehicleId     = nullVehicleId,
                        CustomerID    = customer.CustomersId,
                        Userid        = nullUserId,
                        Traveltime    = dayBooking.TravelTime,
                        BookingType   = dayBType,
                        travelDate    = dayBooking.TravelDate,
                        Status        = Status.Pending,
                        Pax           = dto.Pax,
                        Amount        = dayBooking.Amount,
                        Payment       = paymentSource,
                        TravelAgentId = agent?.AgentId,
                        OrgId         = _tenant.WriteOrgId,
                        CreatedByUserId = _tenant.UserId > 0 ? _tenant.UserId : null,
                    };
                    AssignmentRules.SyncStatus(booking);
                    await _context.Bookings.AddAsync(booking);
                    createdBookings.Add(booking);
                }
                await _context.SaveChangesAsync();

                await UpsertAllocationsAsync(createdBookings.First().BookingId, customer.CustomersId, dto, agent);
                await _context.SaveChangesAsync();
                return createdBookings.First();
            }

            ExternalEmployee? externalEmp = null;
            if (isExternalBooking)
                externalEmp = await UpdateExternalEmployee(dto.ExternalEmployee ?? string.Empty, dto.ExternalEmployeeNumber!);

            Booking target;
            if (existingBooking == null)
            {
                target = new Booking
                {
                    VehicleId       = nullVehicleId,
                    Userid          = nullUserId,
                    OrgId           = _tenant.WriteOrgId,
                    CreatedByUserId = _tenant.UserId > 0 ? _tenant.UserId : null,
                };
                await _context.Bookings.AddAsync(target);
            }
            else
            {
                target = existingBooking;
            }

            target.From               = dto.From;
            target.To                 = dto.To;
            target.VehicleId          = isExternalBooking ? null : nullVehicleId;
            target.Userid             = isExternalBooking ? null : nullUserId;
            target.CustomerID         = customer.CustomersId;
            target.BookingType        = bookingType;
            target.travelDate         = dto.BookingDate;
            target.ReturnDate         = dto.ReturnDate;
            target.Traveltime         = dto.BookingTime;
            target.Status             = bookingStatus;
            target.Pax                = dto.Pax;
            target.Amount             = (decimal)dto.Amount;
            target.Payment            = paymentSource;
            target.ExternalEmployeeId = externalEmp?.externalEmployeeID;
            target.TravelAgentId      = agent?.AgentId;
            // No explicit status from the client: Pending / Assigned follows the driver and vehicle
            if (string.IsNullOrWhiteSpace(dto.BookingStatus)) AssignmentRules.SyncStatus(target);

            await _context.SaveChangesAsync();

            // Allocations first: the settlement balance depends on the customer advance.
            await UpsertAllocationsAsync(target.BookingId, customer.CustomersId, dto, agent);
            await _context.SaveChangesAsync();

            if (externalEmp != null)
            {
                await UpsertExternalSettlement(target.BookingId, externalEmp.externalEmployeeID,
                    target.Amount, (decimal)(dto.commissionAmount ?? 0), dto.Payment);
            }
            else
            {
                // Booking moved back in-house: drop the vendor ledger row if no money moved
                await RemoveUntouchedSettlementAsync(target.BookingId);
            }

            return target;
        }

        /// <summary>
        /// Create/refresh the payment allocations of a booking from the form values.
        /// Previously edits ignored them entirely, and a create with an advance but no
        /// "customer will pay" threw on CustomerWillPay!.Value.
        /// </summary>
        private async Task UpsertAllocationsAsync(int bookingId, int customerId, NewBookiingDTO dto, TravelAgent? agent)
        {
            var allocs = await _context.BookingPaymentAllocations
                .Where(a => a.BookingId == bookingId).ToListAsync();

            decimal advance = dto.AdvancePay ?? 0;
            decimal willPay = dto.CustomerWillPay ?? 0;

            var cust = allocs.FirstOrDefault(a => a.PayerType == PayerType.Customer);
            if (willPay > 0 || advance > 0)
            {
                if (cust == null)
                {
                    cust = new BookingPaymentAllocation { BookingId = bookingId, PayerType = PayerType.Customer };
                    await _context.BookingPaymentAllocations.AddAsync(cust);
                }
                cust.CustomerId      = customerId;
                cust.AllocatedAmount = willPay > 0 ? willPay : advance;
                cust.PaidAmount      = advance;
            }
            else if (cust != null)
            {
                // Advance cleared on the form
                cust.PaidAmount = 0;
                cust.AllocatedAmount = 0;
            }

            decimal ownerWillPay = dto.OwnerWillPay ?? 0;
            if (agent != null)
            {
                // Agent changed: drop allocations for other agents that never received money
                foreach (var stale in allocs.Where(a => a.PayerType != PayerType.Customer
                                                     && a.TravelAgentId != agent.AgentId
                                                     && a.PaidAmount == 0))
                    _context.BookingPaymentAllocations.Remove(stale);

                if (ownerWillPay > 0)
                {
                    var payerType = agent.type == TravelAgentType.TravelOwner ? PayerType.Owner : PayerType.Agent;
                    var mine = allocs.FirstOrDefault(a => a.PayerType != PayerType.Customer && a.TravelAgentId == agent.AgentId);
                    if (mine == null)
                    {
                        await _context.BookingPaymentAllocations.AddAsync(new BookingPaymentAllocation
                        {
                            BookingId       = bookingId,
                            PayerType       = payerType,
                            TravelAgentId   = agent.AgentId,
                            AllocatedAmount = ownerWillPay,
                        });
                    }
                    else
                    {
                        mine.PayerType       = payerType;
                        mine.AllocatedAmount = Math.Max(ownerWillPay, mine.PaidAmount);
                    }
                }
            }
        }

        public async Task<object> FilterBookingsAsync(BookingFilterDTO filterDTO)
        {
            var query = OrgBookings
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Include(b => b.user)
                .Include(b => b.CreatedBy)
                .Include(b => b.ExternalEmployee)
                .Include(b => b.TravelAgent)
                .AsQueryable();

            if (filterDTO.PerticularDate.HasValue) query = query.Where(b => b.travelDate == filterDTO.PerticularDate.Value);
            if (filterDTO.StartDate.HasValue) query = query.Where(b => b.travelDate >= filterDTO.StartDate.Value);
            if (filterDTO.EndDate.HasValue) query = query.Where(b => b.travelDate <= filterDTO.EndDate.Value);
            if (!string.IsNullOrEmpty(filterDTO.From)) query = query.Where(b => b.From == filterDTO.From);
            if (!string.IsNullOrEmpty(filterDTO.To)) query = query.Where(b => b.To == filterDTO.To);
            if (filterDTO.Status.HasValue) query = query.Where(b => b.Status == filterDTO.Status.Value);
            if (filterDTO.VehicleId.HasValue) query = query.Where(b => b.VehicleId == filterDTO.VehicleId.Value);
            if (filterDTO.UserId.HasValue) query = query.Where(b => b.Userid == filterDTO.UserId.Value);
            if (filterDTO.BookingType.HasValue) query = query.Where(b => b.BookingType == filterDTO.BookingType.Value);
            if (filterDTO.TravelTime.HasValue) query = query.Where(b => b.Traveltime == filterDTO.TravelTime.Value);

            var pageSize   = Math.Clamp(filterDTO.PageSize, 1, 200);
            var pageNumber = Math.Max(1, filterDTO.PageNumber);

            var totalCount = await query.CountAsync();
            query = query.OrderByDescending(b => b.travelDate);

            var bookings = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new
            {
                Data = bookings.Select(b => new
                {
                    b.BookingId, b.travelDate, b.Traveltime, b.From, b.To,
                    b.Status, b.Amount, b.Pax, b.BookingType,
                    VehicleName = b.Vehicle?.VehicleName,
                    b.VehicleId,
                    CustomerName = b.Customer?.CustomerName,
                    CustomerNumber = b.Customer?.CustomerNumber,
                    UserName = b.user?.UserName,
                    UserId = b.user?.userId,
                    b.Payment,
                    b.CreatedByUserId,
                    CreatedByName = b.CreatedBy != null ? b.CreatedBy.EmployeeName : null
                }),
                Pagination = new
                {
                    PageNumber = pageNumber,
                    PageSize   = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            };
        }

        public async Task<List<ExternalVendorSettlementDTO>> GetVendorBookingsAsync(int? externalEmployeeId = null)
        {
            // Was unfiltered: every organisation saw every other organisation's vendor ledger.
            var query = OrgSettlements
                .AsNoTracking()
                .Include(x => x.Booking).ThenInclude(b => b.Customer)
                .Include(x => x.ExternalEmployee)
                .AsQueryable();

            if (externalEmployeeId.HasValue)
                query = query.Where(x => x.ExternalEmployeeId == externalEmployeeId.Value);

            var rows = await query.OrderByDescending(x => x.Booking.travelDate).ToListAsync();

            var bookingIds = rows.Select(r => r.BookingId).Distinct().ToList();
            var advances = await _context.BookingPaymentAllocations
                .Where(p => bookingIds.Contains(p.BookingId) && p.PayerType == PayerType.Customer)
                .GroupBy(p => p.BookingId)
                .Select(g => new { g.Key, Paid = g.Sum(x => x.PaidAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Paid);

            return rows.Select(s =>
            {
                var advance   = advances.GetValueOrDefault(s.BookingId, 0);
                var net       = NetPosition(s, advance);
                var due       = Math.Abs(net);
                var settled   = SettledSoFar(s, due);
                var remaining = due - settled;
                var vendorOwes = net > 0;
                var cancelled  = s.Booking.Status == Status.Canceled;

                return new ExternalVendorSettlementDTO
                {
                    BookingId           = s.BookingId,
                    BookingDate         = s.Booking.travelDate,
                    VendorId            = s.ExternalEmployeeId,
                    VendorName          = s.ExternalEmployee?.externalEmployeeName ?? string.Empty,
                    VendorNumber        = s.ExternalEmployee?.externalEmployeeNumber ?? string.Empty,
                    From                = s.Booking.From ?? string.Empty,
                    To                  = s.Booking.To ?? string.Empty,
                    CustomerName        = s.Booking.Customer?.CustomerName ?? string.Empty,
                    BookingAmount       = s.BookingAmount,
                    Commission          = s.CommissionAmount,
                    CashCollectedBy     = s.CashCollectedBy,
                    AdvancePaid         = advance,
                    VendorPayable       = vendorOwes ? 0 : due,
                    TotalPaidToVendor   = vendorOwes ? 0 : settled,
                    PendingVendorPayment = vendorOwes ? 0 : remaining,
                    OwnerReceivable     = vendorOwes ? due : 0,
                    ReceivedFromVendor  = vendorOwes ? settled : 0,
                    PendingReceivable   = vendorOwes ? remaining : 0,
                    Remaining           = remaining,
                    SettlementDirection = vendorOwes ? "VendorToOwner" : "OwnerToVendor",
                    IsSettled           = s.SettledAt != null || remaining <= 0,
                    SettledAt           = s.SettledAt,
                    BookingStatus       = s.Booking.Status.ToString(),
                };
            }).ToList();
        }

        public async Task AssignBookingToExternalVendor(AssignExternalVendorDTO dto)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var booking = await FindOrgBookingAsync(dto.BookingId)
                    ?? throw new KeyNotFoundException("Booking not found");
                if (booking.Status == Status.Canceled)
                    throw new InvalidOperationException("A cancelled booking cannot be assigned.");
                if (string.IsNullOrWhiteSpace(dto.VendorNumber))
                    throw new ArgumentException("Vendor number is required.");
                if (dto.CommissionAmount < 0 || dto.CommissionAmount > booking.Amount)
                    throw new ArgumentException("Commission must be between 0 and the booking amount.");

                var vendor = await UpdateExternalEmployee(dto.VendorName, dto.VendorNumber);
                booking.Userid = null;
                booking.VehicleId = null;
                booking.ExternalEmployeeId = vendor.externalEmployeeID;
                booking.Payment = Payment.ExternalEmployee;
                booking.Status = Status.Assigned;
                await _context.SaveChangesAsync();

                await UpsertExternalSettlement(booking.BookingId, vendor.externalEmployeeID,
                    booking.Amount, dto.CommissionAmount, dto.CashCollectedBy);

                await tx.CommitAsync();
            });
        }

        public async Task SettleSettlementAsync(SettleSettlementDTO dto)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // A retried / double-tapped submit with the same RequestId is recorded once
                if (dto.RequestId.HasValue && await _context.SettlementPayments
                        .AnyAsync(p => p.RequestId == dto.RequestId && p.OrgId == _tenant.WriteOrgId))
                    return;

                await using var tx = await _context.Database.BeginTransactionAsync();

                var booking = await FindOrgBookingAsync(dto.BookingId)
                    ?? throw new KeyNotFoundException("Booking not found");

                var settlement = await _context.ExternalEmployeeCashCollections
                    .FirstOrDefaultAsync(x => x.BookingId == booking.BookingId)
                    ?? throw new KeyNotFoundException("Settlement record not found");

                if (settlement.SettledAt != null)
                    throw new InvalidOperationException("Settlement already completed");

                var net       = NetPosition(settlement, await CustomerAdvanceAsync(booking.BookingId));
                var due       = Math.Abs(net);
                var settled   = SettledSoFar(settlement, due);
                var remaining = due - settled;

                // No amount = settle the full remaining balance. An amount = part payment,
                // added to what was already settled.
                var amount = dto.PaidAmount ?? remaining;
                if (amount <= 0)
                    throw new ArgumentException("Payment amount must be greater than zero.");
                if (amount > remaining + 0.009m)
                    throw new ArgumentException($"Payment of ₹{amount:0.##} is more than the ₹{remaining:0.##} still due.");
                var paidOn = dto.PaidOn ?? DateOnly.FromDateTime(DateTime.Today);
                if (paidOn > DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
                    throw new ArgumentException("Payment date cannot be in the future.");

                settlement.TotalPaidToVendor = settled + amount;
                if (settlement.TotalPaidToVendor >= due)
                {
                    settlement.TotalPaidToVendor = due;
                    settlement.SettledAt = DateTime.UtcNow;
                }

                // Itemised history — every payment is its own row
                _context.SettlementPayments.Add(new SettlementPayment
                {
                    SettlementId       = settlement.Id,
                    BookingId          = booking.BookingId,
                    ExternalEmployeeId = settlement.ExternalEmployeeId,
                    EntryType          = SettlementEntryType.Payment,
                    Direction          = net > 0 ? "VendorToOwner" : "OwnerToVendor",
                    Amount             = Math.Min(amount, remaining),
                    PaymentMethod      = Clean(dto.PaymentMethod),
                    Reference          = Clean(dto.Reference),
                    Notes              = Clean(dto.Notes),
                    PaidOn             = paidOn,
                    RequestId          = dto.RequestId,
                    CreatedByUserId    = _tenant.UserId > 0 ? _tenant.UserId : null,
                    OrgId              = booking.OrgId,
                });

                try
                {
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex) && dto.RequestId.HasValue)
                {
                    // Lost a race with an identical submit — that one was recorded
                    await tx.RollbackAsync();
                    _context.ChangeTracker.Clear();
                }
            });
        }

        /// <summary>
        /// Undo a recorded payment. Financial rows are never deleted: a Reversal row is
        /// added, the original is flagged, and the balance re-opens.
        /// </summary>
        public async Task ReverseSettlementPaymentAsync(int settlementPaymentId, string? reason)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var original = await _context.SettlementPayments
                    .FirstOrDefaultAsync(p => p.SettlementPaymentId == settlementPaymentId)
                    ?? throw new KeyNotFoundException("Payment not found");
                var booking = await FindOrgBookingAsync(original.BookingId)
                    ?? throw new KeyNotFoundException("Payment not found");

                if (original.EntryType == SettlementEntryType.Reversal)
                    throw new InvalidOperationException("A reversal cannot itself be reversed — record a new payment instead.");
                if (original.IsReversed)
                    throw new InvalidOperationException("This payment has already been reversed.");
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException("Please give a reason for the reversal.");

                var settlement = await _context.ExternalEmployeeCashCollections
                    .FirstAsync(s => s.Id == original.SettlementId);
                var due     = Math.Abs(NetPosition(settlement, await CustomerAdvanceAsync(booking.BookingId)));
                var settled = SettledSoFar(settlement, due);

                settlement.TotalPaidToVendor = Math.Max(0, settled - original.Amount);
                if (settlement.TotalPaidToVendor < due) settlement.SettledAt = null;

                original.IsReversed = true;
                _context.SettlementPayments.Add(new SettlementPayment
                {
                    SettlementId       = original.SettlementId,
                    BookingId          = original.BookingId,
                    ExternalEmployeeId = original.ExternalEmployeeId,
                    EntryType          = SettlementEntryType.Reversal,
                    Direction          = original.Direction,
                    Amount             = original.Amount,
                    PaymentMethod      = original.PaymentMethod,
                    Notes              = reason.Trim(),
                    PaidOn             = DateOnly.FromDateTime(DateTime.Today),
                    ReversesPaymentId  = original.SettlementPaymentId,
                    CreatedByUserId    = _tenant.UserId > 0 ? _tenant.UserId : null,
                    OrgId              = original.OrgId,
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            });
        }

        public async Task<SettlementHistoryDTO> GetSettlementHistoryAsync(int bookingId)
        {
            var booking = await FindOrgBookingAsync(bookingId)
                ?? throw new KeyNotFoundException("Booking not found");
            var settlement = await _context.ExternalEmployeeCashCollections.AsNoTracking()
                .FirstOrDefaultAsync(s => s.BookingId == bookingId)
                ?? throw new KeyNotFoundException("This booking has no partner settlement");

            var net     = NetPosition(settlement, await CustomerAdvanceAsync(bookingId));
            var due     = Math.Abs(net);
            var settled = SettledSoFar(settlement, due);
            var rows    = await PaymentRowsAsync(new[] { bookingId });
            var itemised = rows.Sum(r => r.EntryType == nameof(SettlementEntryType.Reversal) ? -r.Amount : r.Amount);

            return new SettlementHistoryDTO
            {
                BookingId = bookingId,
                Direction = net > 0 ? "VendorToOwner" : "OwnerToVendor",
                Due = due,
                Settled = settled,
                Remaining = due - settled,
                EarlierUnitemised = Math.Max(0, settled - itemised),
                IsSettled = settlement.SettledAt != null || due - settled <= 0,
                Payments = rows,
            };
        }

        private async Task<List<SettlementPaymentDTO>> PaymentRowsAsync(IEnumerable<int> bookingIds)
        {
            var ids = bookingIds.ToList();
            var rows = await _context.SettlementPayments.AsNoTracking()
                .Where(p => ids.Contains(p.BookingId))
                .OrderBy(p => p.PaidOn).ThenBy(p => p.SettlementPaymentId)
                .ToListAsync();
            var userIds = rows.Where(r => r.CreatedByUserId != null).Select(r => r.CreatedByUserId!.Value).Distinct().ToList();
            var names = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.userId))
                .ToDictionaryAsync(u => u.userId, u => u.EmployeeName);

            return rows.Select(r => new SettlementPaymentDTO
            {
                SettlementPaymentId = r.SettlementPaymentId,
                BookingId     = r.BookingId,
                EntryType     = r.EntryType.ToString(),
                Direction     = r.Direction,
                Amount        = r.Amount,
                PaymentMethod = r.PaymentMethod,
                Reference     = r.Reference,
                Notes         = r.Notes,
                PaidOn        = r.PaidOn,
                ReversesPaymentId = r.ReversesPaymentId,
                IsReversed    = r.IsReversed,
                CreatedAt     = r.CreatedAt,
                CreatedBy     = r.CreatedByUserId is int uid && names.TryGetValue(uid, out var n) ? n : null,
            }).ToList();
        }

        /// <summary>
        /// Data for a partner statement: their bookings in the period (by travel date) and
        /// every payment recorded against those bookings. Only this partner, only this org.
        /// </summary>
        public async Task<PartnerStatementData> GetPartnerStatementAsync(
            int partnerId, DateOnly? from, DateOnly? to, int? bookingId, bool maskCustomer, bool showFare)
        {
            var partner = await _context.ExternalEmployees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.externalEmployeeID == partnerId);
            if (partner == null || !_tenant.CanAccess(partner.OrgId))
                throw new KeyNotFoundException("Partner not found");

            var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.OrgId == partner.OrgId);

            var q = _context.ExternalEmployeeCashCollections.AsNoTracking()
                .Include(s => s.Booking).ThenInclude(b => b.Customer)
                .Where(s => s.ExternalEmployeeId == partnerId && s.Booking.OrgId == partner.OrgId);
            if (bookingId.HasValue) q = q.Where(s => s.BookingId == bookingId.Value);
            if (from.HasValue) q = q.Where(s => s.Booking.travelDate >= from.Value);
            if (to.HasValue) q = q.Where(s => s.Booking.travelDate <= to.Value);

            var settlements = await q.OrderBy(s => s.Booking.travelDate).ThenBy(s => s.BookingId).ToListAsync();
            if (bookingId.HasValue && settlements.Count == 0)
                throw new KeyNotFoundException("That booking is not assigned to this partner");

            var ids = settlements.Select(s => s.BookingId).ToList();
            var advances = await _context.BookingPaymentAllocations.AsNoTracking()
                .Where(p => ids.Contains(p.BookingId) && p.PayerType == PayerType.Customer)
                .GroupBy(p => p.BookingId)
                .Select(g => new { g.Key, Paid = g.Sum(x => x.PaidAmount) })
                .ToDictionaryAsync(x => x.Key, x => x.Paid);
            var payments = await PaymentRowsAsync(ids);

            var data = new PartnerStatementData
            {
                OrgName       = org?.Name ?? "",
                OrgPhone      = org?.Phone,
                OrgEmail      = org?.Email,
                OrgAddress    = org?.Address,
                PartnerId     = partner.externalEmployeeID,
                PartnerName   = partner.externalEmployeeName ?? "",
                PartnerNumber = partner.externalEmployeeNumber ?? "",
                From = from, To = to, BookingId = bookingId,
                ShowFare      = showFare,
                GeneratedAt   = DateTime.Now,
                Payments      = payments,
            };

            foreach (var s in settlements)
            {
                var cancelled = s.Booking.Status == Status.Canceled;
                // A cancelled booking with no money moved owes nothing — leave it off
                if (cancelled && s.SettledAt == null && s.TotalPaidToVendor == 0) continue;

                var net     = NetPosition(s, advances.GetValueOrDefault(s.BookingId, 0));
                var due     = Math.Abs(net);
                var settled = SettledSoFar(s, due);
                var remaining = cancelled ? 0 : due - settled;

                data.Lines.Add(new PartnerStatementLine
                {
                    BookingId  = s.BookingId,
                    TravelDate = s.Booking.travelDate,
                    Route      = $"{s.Booking.From} → {s.Booking.To}",
                    Customer   = maskCustomer ? MaskName(s.Booking.Customer?.CustomerName) : (s.Booking.Customer?.CustomerName ?? ""),
                    Fare       = s.BookingAmount,
                    Commission = s.CommissionAmount,
                    PartnerEarning = cancelled ? 0 : Math.Max(0, s.BookingAmount - s.CommissionAmount),
                    CashCollectedBy = s.CashCollectedBy == CashCollectedBy.Admin ? "Us" : "Partner",
                    Direction  = net > 0 ? "VendorToOwner" : "OwnerToVendor",
                    Due        = due,
                    Settled    = settled,
                    Remaining  = remaining,
                    Cancelled  = cancelled,
                });

                if (!cancelled)
                {
                    data.TotalFare += s.BookingAmount;
                    data.TotalCommission += s.CommissionAmount;
                    data.TotalPartnerEarning += Math.Max(0, s.BookingAmount - s.CommissionAmount);
                }
                if (net > 0) { data.ReceivedFromPartner += settled; data.PartnerStillOwes += remaining; }
                else         { data.PaidToPartner += settled;       data.WeStillOwe       += remaining; }
            }

            var itemised = payments.Sum(p => p.EntryType == nameof(SettlementEntryType.Reversal) ? -p.Amount : p.Amount);
            data.EarlierUnitemised = Math.Max(0, data.ReceivedFromPartner + data.PaidToPartner - itemised);
            return data;
        }

        /// <summary>"Aarav Mehta &amp; Family" → "Aarav M." — partners don't need full customer names.</summary>
        private static string MaskName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "—";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1 ? parts[0] : $"{parts[0]} {char.ToUpperInvariant(parts[1][0])}.";
        }

        private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        internal static string DigitsOf(string? v) => new string((v ?? "").Where(char.IsDigit).ToArray());

        /// <summary>
        /// Ways the same Indian mobile number gets stored ("+91 98765 00001", "919876500001",
        /// "09876500001", "9876500001"), so one person is not saved as several customers.
        /// </summary>
        internal static List<string> PhoneVariants(string? raw)
        {
            var set = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(raw)) set.Add(raw.Trim());
            var digits = DigitsOf(raw);
            if (digits.Length >= 10)
            {
                var last10 = digits[^10..];
                var split = last10[..5] + " " + last10[5..];   // "98765 00001", how numbers are often pasted
                foreach (var v in new[] { last10, "0" + last10, "91" + last10, "+91" + last10, "+91 " + last10, digits,
                                          split, "+91 " + split, "+91" + split, "0" + split })
                    set.Add(v);
            }
            return set.ToList();
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };

        private async Task<ExternalEmployee> UpdateExternalEmployee(string name, string number)
        {
            // Vendors are per organisation — matching on phone number alone reused
            // (and renamed) another org's vendor record.
            var orgId = _tenant.WriteOrgId;
            var variants = PhoneVariants(number);
            var vendor = await _context.ExternalEmployees
                .Where(x => x.OrgId == orgId && x.externalEmployeeNumber != null && variants.Contains(x.externalEmployeeNumber))
                .OrderBy(x => x.externalEmployeeID)
                .FirstOrDefaultAsync();

            if (vendor != null)
            {
                if (!string.IsNullOrWhiteSpace(name) && vendor.externalEmployeeName != name)
                {
                    vendor.externalEmployeeName = name;
                    await _context.SaveChangesAsync();
                }
                return vendor;
            }

            vendor = new ExternalEmployee
            {
                externalEmployeeName   = name,
                externalEmployeeNumber = number,
                OrgId = orgId,
            };
            await _context.ExternalEmployees.AddAsync(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        private async Task<Customers> UpdateCustomer(NewBookiingDTO dto)
        {
            // Customers are per organisation — matching on phone number alone let one
            // org overwrite another org's customer name.
            var orgId = _tenant.WriteOrgId;
            var variants = PhoneVariants(dto.CustomerNumber);
            var customer = await _context.Customers
                .Where(x => x.OrgId == orgId && variants.Contains(x.CustomerNumber))
                .OrderBy(x => x.CustomersId)
                .FirstOrDefaultAsync();
            if (customer == null)
            {
                customer = new Customers
                {
                    CustomerName    = dto.CustomerName ?? string.Empty,
                    CustomerNumber  = dto.CustomerNumber,
                    AlternateNumber = dto.AlternateNumber,
                    TravelDate      = dto.BookingDate,
                    OrgId           = orgId,
                };
                await _context.Customers.AddAsync(customer);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.CustomerName)) customer.CustomerName = dto.CustomerName;
                customer.AlternateNumber = dto.AlternateNumber ?? customer.AlternateNumber;
                customer.TravelDate = dto.BookingDate;
            }
            await _context.SaveChangesAsync();
            return customer;
        }

        private async Task RemoveUntouchedSettlementAsync(int bookingId)
        {
            var stale = await _context.ExternalEmployeeCashCollections
                .Where(s => s.BookingId == bookingId && s.SettledAt == null && s.TotalPaidToVendor == 0)
                .ToListAsync();
            if (stale.Count == 0) return;
            _context.ExternalEmployeeCashCollections.RemoveRange(stale);
            await _context.SaveChangesAsync();
        }

        private async Task UpsertExternalSettlement(int bookingId, int externalEmployeeId,
            decimal bookingAmount, decimal commissionAmount, string collectedBy)
        {
            var settlement = await _context.ExternalEmployeeCashCollections
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            CashCollectedBy cashCollectedBy = collectedBy == "ExternalEmployee"
                ? CashCollectedBy.ExternalEmployee : CashCollectedBy.Admin;

            var advance = await CustomerAdvanceAsync(bookingId);

            if (settlement == null)
            {
                settlement = new ExternalEmployeeCashCollection
                {
                    BookingId          = bookingId,
                    ExternalEmployeeId = externalEmployeeId,
                    BookingAmount      = bookingAmount,
                    CommissionAmount   = commissionAmount,
                    CashCollectedBy    = cashCollectedBy,
                    CreatedAt          = DateTime.UtcNow,
                    TotalPaidToVendor  = 0,
                };
                // Nothing due (e.g. the advance Admin took equals the commission) → settled
                if (NetPosition(settlement, advance) == 0)
                    settlement.SettledAt = DateTime.UtcNow;
                await _context.ExternalEmployeeCashCollections.AddAsync(settlement);
            }
            else
            {
                // Keep the money that has already moved; only the amounts change.
                // Editing a booking used to clear SettledAt and silently re-open fully
                // paid settlements.
                var oldDue  = Math.Abs(NetPosition(settlement, advance));
                var settled = SettledSoFar(settlement, oldDue);

                settlement.ExternalEmployeeId = externalEmployeeId;
                settlement.BookingAmount      = bookingAmount;
                settlement.CommissionAmount   = commissionAmount;
                settlement.CashCollectedBy    = cashCollectedBy;

                var newDue = Math.Abs(NetPosition(settlement, advance));
                settlement.TotalPaidToVendor = Math.Min(settled, newDue);
                settlement.SettledAt = settled >= newDue
                    ? settlement.SettledAt ?? DateTime.UtcNow
                    : null;
            }
            await _context.SaveChangesAsync();
        }
    }
}
