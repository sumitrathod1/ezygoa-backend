using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class BookingRepository : IBookingRepository
    {
        private readonly AppDbContext _context;

        public BookingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Booking?> GetByIdAsync(int id)
        {
            return await _context.Bookings.FindAsync(id);
        }

        public async Task<List<ExternalEmployee>> GetExternalEmployeesAsync()
        {
            return await _context.ExternalEmployees.ToListAsync();
        }

        public async Task<object> GetAllBookingsWithStatsAsync()
        {
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.user)
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Where(b => b.Status != Status.Canceled)
                .OrderByDescending(b => b.travelDate)
                .ToListAsync();

            var payments = await _context.BookingPaymentAllocations
                .GroupBy(p => p.BookingId)
                .Select(g => new
                {
                    BookingId = g.Key,
                    TotalAllocated = g.Sum(x => x.AllocatedAmount),
                    TotalPaid = g.Where(x => x.PayerType == PayerType.Customer).Sum(x => x.PaidAmount)
                })
                .ToListAsync();

            var paymentDict = payments.ToDictionary(p => p.BookingId, p => p);

            var bookingsWithPayment = bookings.Select(b =>
            {
                paymentDict.TryGetValue(b.BookingId, out var pay);
                return new
                {
                    b.BookingId, b.travelDate, b.From, b.To, b.VehicleId, b.Vehicle,
                    b.Userid, b.user, b.BookingType, b.Traveltime, b.Status, b.Amount,
                    b.Pax, b.Assigned, b.CustomerID, b.Customer, b.ExternalEmployeeId,
                    b.ExternalEmployee, b.Payment, b.TravelAgentId, b.TravelAgent,
                    b.Payments, b.isValidAssignment,
                    AdvancePaid = pay?.TotalPaid ?? 0,
                    TotalAllocated = pay?.TotalAllocated ?? 0,
                    Balance = b.Amount - (pay?.TotalPaid ?? 0)
                };
            }).ToList();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var currentMonth = today.Month;
            var currentYear = today.Year;
            var totalToday = bookings.Where(b => b.travelDate == today).Sum(b => b.Amount);

            var dayOfWeek = (int)today.DayOfWeek;
            var daysSinceMonday = (dayOfWeek == 0) ? 6 : dayOfWeek - 1;
            var weekStart = today.AddDays(-daysSinceMonday);
            var weekEnd = weekStart.AddDays(6);

            var totalWeek = bookings
                .Where(b => b.travelDate >= weekStart && b.travelDate <= weekEnd
                    && b.travelDate.Month == currentMonth && b.travelDate.Year == currentYear)
                .Sum(b => b.Amount);

            var totalMonth = bookings
                .Where(b => b.travelDate.Month == currentMonth && b.travelDate.Year == currentYear)
                .Sum(b => b.Amount);

            var totalYear = bookings.Where(b => b.travelDate.Year == currentYear).Sum(b => b.Amount);
            var totalRevenue = bookings.Sum(b => b.Amount);
            var isLeapYear = DateTime.IsLeapYear(currentYear);
            int daysInYear = isLeapYear ? 366 : 365;

            return new
            {
                bookings = bookingsWithPayment,
                revenueStats = new
                {
                    today = totalToday,
                    week = totalWeek,
                    month = totalMonth,
                    year = totalYear,
                    total = totalRevenue
                },
                averageStats = new
                {
                    dailyAvg = daysInYear > 0 ? Math.Round(totalYear / (decimal)daysInYear, 2) : 0,
                    weeklyAvg = Math.Round(totalYear / 52m, 2),
                    monthlyAvg = Math.Round(totalYear / 12m, 2),
                    yearlyAvg = totalYear
                }
            };
        }

        public async Task<bool> CancelBookingAsync(int bookingId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId);
                if (booking == null) return false;

                booking.Status = Status.Canceled;
                booking.Amount = 0;

                var payments = await _context.Payments.Where(p => p.BookingId == bookingId).ToListAsync();
                foreach (var payment in payments) payment.AmountPaid = 0;

                var allocations = await _context.BookingPaymentAllocations
                    .Where(a => a.BookingId == bookingId).ToListAsync();
                foreach (var allocation in allocations)
                {
                    allocation.AllocatedAmount = 0;
                    allocation.PaidAmount = 0;
                }

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
        }

        public async Task<Booking> CreateBooking(NewBookiingDTO dto)
        {
            int? nullVehicleId = dto.VehicleId > 0 ? dto.VehicleId : null;
            int? nullUserId = dto.UserId > 0 ? dto.UserId : null;
            bool isExternalBooking = !string.IsNullOrWhiteSpace(dto.ExternalEmployeeNumber);

            BookingType bookingType = Enum.TryParse<BookingType>(dto.BookingType, out var bType)
                ? bType : BookingType.Notspecified;
            Status bookingStatus = Enum.TryParse<Status>(dto.BookingStatus, out var status)
                ? status : Status.Pending;
            Payment paymentSource = dto.Payment == "ExternalEmployee"
                ? Payment.ExternalEmployee : Payment.Admin;

            var existingBooking = dto.BookingId.HasValue
                ? await _context.Bookings.FirstOrDefaultAsync(x => x.BookingId == dto.BookingId)
                : null;

            TravelAgent? agent = null;
            if (dto.TravelAgentId != null)
                agent = await _context.TravelAgents.FirstOrDefaultAsync(x => x.AgentId == dto.TravelAgentId);

            await UpdateCustomer(dto);
            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.CustomerNumber == dto.CustomerNumber);

            if (dto.DayWiseBookings != null && dto.DayWiseBookings.Count > 1)
            {
                List<Booking> createdBookings = new();
                foreach (var dayBooking in dto.DayWiseBookings)
                {
                    BookingType dayBType = Enum.TryParse<BookingType>(dayBooking.BookingType, out var bt)
                        ? bt : BookingType.Notspecified;

                    var booking = new Booking
                    {
                        From = dayBooking.From,
                        To = dayBooking.To,
                        VehicleId = nullVehicleId,
                        CustomerID = customer!.CustomersId,
                        Userid = nullUserId,
                        Traveltime = dayBooking.TravelTime,
                        BookingType = dayBType,
                        travelDate = dayBooking.TravelDate,
                        Status = Status.Pending,
                        Pax = dto.Pax,
                        Amount = dayBooking.Amount,
                        Payment = paymentSource,
                        TravelAgentId = agent?.AgentId
                    };
                    await _context.Bookings.AddAsync(booking);
                    createdBookings.Add(booking);
                }
                await _context.SaveChangesAsync();

                var allocations = new List<BookingPaymentAllocation>();
                if ((dto.CustomerWillPay ?? 0) > 0 || (dto.AdvancePay ?? 0) > 0)
                {
                    allocations.Add(new BookingPaymentAllocation
                    {
                        BookingId = createdBookings.First().BookingId,
                        PayerType = PayerType.Customer,
                        CustomerId = customer!.CustomersId,
                        AllocatedAmount = dto.CustomerWillPay > 0 ? dto.CustomerWillPay!.Value : (dto.AdvancePay ?? 0),
                        PaidAmount = dto.AdvancePay ?? 0
                    });
                }
                if (dto.OwnerWillPay > 0 && agent != null)
                {
                    var payerType = agent.type == TravelAgentType.TravelOwner ? PayerType.Owner : PayerType.Agent;
                    allocations.Add(new BookingPaymentAllocation
                    {
                        BookingId = createdBookings.First().BookingId,
                        PayerType = payerType,
                        TravelAgentId = agent.AgentId,
                        AllocatedAmount = dto.OwnerWillPay ?? 0
                    });
                }
                if (allocations.Any())
                {
                    await _context.BookingPaymentAllocations.AddRangeAsync(allocations);
                    await _context.SaveChangesAsync();
                }
                return createdBookings.First();
            }

            if (existingBooking == null)
            {
                if (dto.ExternalEmployee != null)
                    await UpdateExternalEmployee(dto.ExternalEmployee, dto.ExternalEmployeeNumber!);

                var externalEmp = await _context.ExternalEmployees
                    .FirstOrDefaultAsync(x => x.externalEmployeeNumber == dto.ExternalEmployeeNumber);

                var newBooking = new Booking
                {
                    From = dto.From,
                    To = dto.To,
                    VehicleId = nullVehicleId,
                    CustomerID = customer!.CustomersId,
                    Userid = nullUserId,
                    Traveltime = dto.BookingTime,
                    BookingType = bookingType,
                    travelDate = dto.BookingDate,
                    Status = bookingStatus,
                    Pax = dto.Pax,
                    ExternalEmployeeId = isExternalBooking ? externalEmp?.externalEmployeeID : null,
                    Amount = (decimal)dto.Amount,
                    Payment = paymentSource,
                    TravelAgentId = agent?.AgentId
                };

                await _context.Bookings.AddAsync(newBooking);
                await _context.SaveChangesAsync();

                if (isExternalBooking && externalEmp != null)
                    await UpsertExternalSettlement(newBooking.BookingId, externalEmp.externalEmployeeID,
                        (decimal)dto.Amount, (decimal)(dto.commissionAmount ?? 0), dto.Payment, dto.AdvancePay);

                var allocations = new List<BookingPaymentAllocation>();
                if (dto.CustomerWillPay > 0 || (dto.AdvancePay ?? 0) > 0)
                {
                    allocations.Add(new BookingPaymentAllocation
                    {
                        BookingId = newBooking.BookingId,
                        PayerType = PayerType.Customer,
                        CustomerId = customer.CustomersId,
                        AllocatedAmount = dto.CustomerWillPay!.Value,
                        PaidAmount = dto.AdvancePay ?? 0
                    });
                }
                if (dto.OwnerWillPay > 0 && agent != null)
                {
                    var payerType = agent.type == TravelAgentType.TravelOwner ? PayerType.Owner : PayerType.Agent;
                    allocations.Add(new BookingPaymentAllocation
                    {
                        BookingId = newBooking.BookingId,
                        PayerType = payerType,
                        TravelAgentId = agent.AgentId,
                        AllocatedAmount = dto.OwnerWillPay!.Value
                    });
                }
                if (allocations.Any())
                {
                    await _context.BookingPaymentAllocations.AddRangeAsync(allocations);
                    await _context.SaveChangesAsync();
                }
                return newBooking;
            }
            else
            {
                if (dto.ExternalEmployee != null)
                    await UpdateExternalEmployee(dto.ExternalEmployee, dto.ExternalEmployeeNumber!);

                var externalEmp = await _context.ExternalEmployees
                    .FirstOrDefaultAsync(x => x.externalEmployeeNumber == dto.ExternalEmployeeNumber);

                existingBooking.From = dto.From;
                existingBooking.To = dto.To;
                existingBooking.VehicleId = nullVehicleId;
                existingBooking.Userid = nullUserId;
                existingBooking.CustomerID = customer!.CustomersId;
                existingBooking.BookingType = bookingType;
                existingBooking.travelDate = dto.BookingDate;
                existingBooking.Traveltime = dto.BookingTime;
                existingBooking.Status = bookingStatus;
                existingBooking.Pax = dto.Pax;
                existingBooking.Amount = (decimal)dto.Amount;
                existingBooking.Payment = paymentSource;
                existingBooking.ExternalEmployeeId = isExternalBooking ? externalEmp?.externalEmployeeID : null;
                existingBooking.TravelAgentId = agent?.AgentId;

                _context.Bookings.Update(existingBooking);
                await _context.SaveChangesAsync();

                if (isExternalBooking && externalEmp != null)
                    await UpsertExternalSettlement(existingBooking.BookingId, externalEmp.externalEmployeeID,
                        (decimal)dto.Amount, (decimal)(dto.commissionAmount ?? 0), dto.Payment, dto.AdvancePay);

                return existingBooking;
            }
        }

        public async Task<object> FilterBookingsAsync(BookingFilterDTO filterDTO)
        {
            var query = _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Include(b => b.user)
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

            var totalCount = await query.CountAsync();
            query = query.OrderByDescending(b => b.travelDate);

            var bookings = await query
                .Skip((filterDTO.PageNumber - 1) * filterDTO.PageSize)
                .Take(filterDTO.PageSize)
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
                    b.Payment
                }),
                Pagination = new
                {
                    filterDTO.PageNumber,
                    filterDTO.PageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filterDTO.PageSize)
                }
            };
        }

        public async Task<List<ExternalVendorSettlementDTO>> GetVendorBookingsAsync(int? externalEmployeeId = null)
        {
            var query = _context.ExternalEmployeeCashCollections
                .Include(x => x.Booking).ThenInclude(b => b.Customer)
                .Include(x => x.ExternalEmployee)
                .AsQueryable();

            if (externalEmployeeId.HasValue)
                query = query.Where(x => x.ExternalEmployeeId == externalEmployeeId.Value);

            var result = await query
                .OrderByDescending(x => x.Booking.travelDate)
                .Select(x => new
                {
                    Settlement = x,
                    AdvancePaid = _context.BookingPaymentAllocations
                        .Where(p => p.BookingId == x.BookingId && p.PayerType == PayerType.Customer)
                        .Select(p => p.PaidAmount).FirstOrDefault()
                })
                .Select(z => new ExternalVendorSettlementDTO
                {
                    BookingId = z.Settlement.BookingId,
                    BookingDate = z.Settlement.Booking.travelDate,
                    VendorName = z.Settlement.ExternalEmployee!.externalEmployeeName!,
                    VendorNumber = z.Settlement.ExternalEmployee.externalEmployeeNumber!,
                    From = z.Settlement.Booking.From!,
                    To = z.Settlement.Booking.To!,
                    CustomerName = z.Settlement.Booking.Customer!.CustomerName,
                    BookingAmount = z.Settlement.BookingAmount,
                    Commission = z.Settlement.CommissionAmount,
                    CashCollectedBy = z.Settlement.CashCollectedBy,
                    VendorPayable = z.Settlement.CashCollectedBy == CashCollectedBy.Admin
                        ? (z.Settlement.BookingAmount - z.Settlement.CommissionAmount) : 0,
                    TotalPaidToVendor = z.Settlement.CashCollectedBy == CashCollectedBy.Admin
                        ? z.Settlement.TotalPaidToVendor : 0,
                    PendingVendorPayment = z.Settlement.CashCollectedBy == CashCollectedBy.Admin
                        ? (z.Settlement.BookingAmount - z.Settlement.CommissionAmount - z.Settlement.TotalPaidToVendor) : 0,
                    OwnerReceivable = z.Settlement.CashCollectedBy == CashCollectedBy.ExternalEmployee
                        ? Math.Max(0, z.Settlement.CommissionAmount - z.AdvancePaid) : 0,
                    SettlementDirection = z.Settlement.CashCollectedBy == CashCollectedBy.ExternalEmployee
                        ? "VendorToOwner" : "OwnerToVendor",
                    IsSettled = z.Settlement.IsSettled,
                    SettledAt = z.Settlement.SettledAt
                })
                .ToListAsync();

            return result;
        }

        public async Task AssignBookingToExternalVendor(AssignExternalVendorDTO dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == dto.BookingId);
            if (booking == null) throw new Exception("Booking not found");

            var vendor = await UpdateExternalEmployee(dto.VendorName, dto.VendorNumber);
            booking.Userid = null;
            booking.VehicleId = null;
            booking.ExternalEmployeeId = vendor.externalEmployeeID;
            booking.Payment = Payment.ExternalEmployee;
            booking.Status = Status.Assigned;

            _context.Bookings.Update(booking);
            await UpsertExternalSettlement(booking.BookingId, vendor.externalEmployeeID,
                booking.Amount, dto.CommissionAmount, dto.CashCollectedBy, dto.AdvancePay);

            await tx.CommitAsync();
        }

        public async Task SettleSettlementAsync(SettleSettlementDTO dto)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var settlement = await _context.ExternalEmployeeCashCollections
                .FirstOrDefaultAsync(x => x.BookingId == dto.BookingId);

            if (settlement == null) throw new Exception("Settlement record not found");
            if (settlement.IsSettled) throw new Exception("Settlement already completed");

            if (settlement.CashCollectedBy == CashCollectedBy.Admin)
                settlement.TotalPaidToVendor = dto.PaidAmount ?? settlement.PayableToVendor;
            else
                settlement.TotalPaidToVendor = settlement.PayableToVendor;

            settlement.SettledAt = DateTime.UtcNow;
            _context.ExternalEmployeeCashCollections.Update(settlement);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }

        private async Task<ExternalEmployee> UpdateExternalEmployee(string name, string number)
        {
            var vendor = await _context.ExternalEmployees
                .FirstOrDefaultAsync(x => x.externalEmployeeNumber == number);

            if (vendor != null)
            {
                if (!string.IsNullOrWhiteSpace(name) && vendor.externalEmployeeName != name)
                {
                    vendor.externalEmployeeName = name;
                    await _context.SaveChangesAsync();
                }
                return vendor;
            }

            vendor = new ExternalEmployee { externalEmployeeName = name, externalEmployeeNumber = number };
            await _context.ExternalEmployees.AddAsync(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        private async Task UpdateCustomer(NewBookiingDTO dto)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(x => x.CustomerNumber == dto.CustomerNumber);
            if (customer == null)
            {
                await _context.Customers.AddAsync(new Customers
                {
                    CustomerName = dto.CustomerName ?? string.Empty,
                    CustomerNumber = dto.CustomerNumber,
                    AlternateNumber = dto.AlternateNumber,
                    TravelDate = dto.BookingDate
                });
            }
            else
            {
                customer.CustomerName = dto.CustomerName ?? string.Empty;
                customer.AlternateNumber = dto.AlternateNumber;
                customer.TravelDate = dto.BookingDate;
                _context.Entry(customer).State = EntityState.Modified;
            }
            await _context.SaveChangesAsync();
        }

        private async Task UpsertExternalSettlement(int bookingId, int externalEmployeeId,
            decimal bookingAmount, decimal commissionAmount, string collectedBy, decimal? advancePay)
        {
            var settlement = await _context.ExternalEmployeeCashCollections
                .FirstOrDefaultAsync(x => x.BookingId == bookingId);

            CashCollectedBy cashCollectedBy = collectedBy == "ExternalEmployee"
                ? CashCollectedBy.ExternalEmployee : CashCollectedBy.Admin;

            bool commissionAlreadyTaken = advancePay.HasValue && advancePay.Value >= commissionAmount
                && cashCollectedBy == CashCollectedBy.ExternalEmployee;

            if (settlement == null)
            {
                settlement = new ExternalEmployeeCashCollection
                {
                    BookingId = bookingId,
                    ExternalEmployeeId = externalEmployeeId,
                    BookingAmount = bookingAmount,
                    CommissionAmount = commissionAmount,
                    CashCollectedBy = cashCollectedBy,
                    CreatedAt = DateTime.UtcNow,
                    TotalPaidToVendor = commissionAlreadyTaken ? bookingAmount - commissionAmount : 0,
                    SettledAt = commissionAlreadyTaken ? DateTime.UtcNow : null
                };
                await _context.ExternalEmployeeCashCollections.AddAsync(settlement);
            }
            else
            {
                settlement.ExternalEmployeeId = externalEmployeeId;
                settlement.BookingAmount = bookingAmount;
                settlement.CommissionAmount = commissionAmount;
                settlement.CashCollectedBy = cashCollectedBy;
                if (commissionAlreadyTaken)
                {
                    settlement.TotalPaidToVendor = bookingAmount - commissionAmount;
                    settlement.SettledAt = DateTime.UtcNow;
                }
                else
                    settlement.SettledAt = null;
            }
            await _context.SaveChangesAsync();
        }
    }
}