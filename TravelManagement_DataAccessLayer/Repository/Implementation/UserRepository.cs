using Microsoft.EntityFrameworkCore;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Entities;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.DataAccessLayer.Repository.Implementation
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> FindByUsernameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(x => x.UserName == username);
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<List<User>> GetAllActiveUsersAsync()
        {
            return await _context.Users.Where(u => u.Status && !u.IsDeleted).ToListAsync();
        }

        public async Task<List<User>> GetAllUsersIncludingDeletedAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<List<User>> GetAvailableDriversAsync(DateOnly date)
        {
            var drivers = await _context.Users
                .Where(u => u.Status && !u.IsDeleted && u.Licence != null)
                .ToListAsync();

            var busyIds = await _context.Bookings
                .Where(b => b.travelDate == date && b.Status != Status.Canceled)
                .Select(b => b.Userid)
                .Distinct()
                .ToListAsync();

            return drivers.Where(d => !busyIds.Contains(d.userId)).ToList();
        }

        public async Task SaveFcmTokenAsync(int userId, string fcmToken)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new Exception("User not found");
            user.FcmToken = fcmToken;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> UpdateUserProfileAsync(int id, UpdateUserDTO dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return null;

            if (!string.IsNullOrWhiteSpace(dto.EmployeeName)) user.EmployeeName = dto.EmployeeName;
            if (!string.IsNullOrWhiteSpace(dto.UserName))     user.UserName     = dto.UserName;
            if (dto.Address    != null) user.Address    = dto.Address;
            if (dto.Email      != null) user.Email      = dto.Email;
            if (dto.Number     != null) user.Number     = dto.Number;
            if (dto.BankAccount!= null) user.BankAccount= dto.BankAccount;
            if (dto.EmployeeDOB.HasValue)
            {
                user.EmployeeDOB = dto.EmployeeDOB;
                user.EmployeAge  = (int)Calculations.CalculateAge(dto.EmployeeDOB.Value, DateTime.Now);
            }
            if (!string.IsNullOrWhiteSpace(dto.Password))
                user.Password = PasswordHasher.HashPassword(dto.Password);

            user.Role           = (Role)dto.Role;
            user.Licence        = dto.Licence.HasValue ? (Licecnce?)dto.Licence.Value : null;
            user.Salary         = dto.Salary;
            user.SalaryDay      = dto.SalaryDay > 0 ? dto.SalaryDay : 1;
            user.IsSalaryActive = dto.IsSalaryActive;

            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> NewUser(User user)
        {
            user.Status = true;
            user.Password = PasswordHasher.HashPassword(user.Password);
            if (user.EmployeeDOB.HasValue)
                user.EmployeAge = (int)Calculations.CalculateAge(user.EmployeeDOB.Value, DateTime.Now);

            await _context.AddAsync(user);
            await _context.SaveChangesAsync();
            await AddSalary(user.Salary, user.userId);
            return user;
        }

        public async Task<bool> DeleteUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.userId == id);
            if (user == null) return false;
            user.Status    = false;
            user.IsDeleted = true;
            user.DeletedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RestoreUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.userId == id);
            if (user == null) return false;
            user.IsDeleted = false;
            user.Status    = true;
            user.DeletedAt = null;
            user.DeletedBy = null;
            await _context.SaveChangesAsync();
            return true;
        }

        public int FindBookingId(int id, DateOnly selectedDate, Status bookingStatus)
        {
            var booking = _context.Bookings
                .Where(x => x.Userid == id && x.Status == bookingStatus && x.travelDate == selectedDate)
                .FirstOrDefault();
            if (booking == null) return -1;
            return booking.Userid ?? -1;
        }

        public async Task<IEnumerable<object>> GetBookingsWithPaymentsByUserIdAsync(int userId)
        {
            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.Vehicle)
                .Where(b => b.Userid == userId && b.Status != Status.Canceled)
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

            return bookings.Select(b =>
            {
                paymentDict.TryGetValue(b.BookingId, out var pay);
                return (object)new
                {
                    b.BookingId, b.travelDate, b.Traveltime, b.From, b.To,
                    b.VehicleId, b.BookingType, b.Vehicle, b.Amount, b.Status, b.Customer,
                    AdvancePaid = pay?.TotalPaid ?? 0,
                    TotalAllocated = pay?.TotalAllocated ?? 0,
                    Balance = b.Amount - (pay?.TotalPaid ?? 0)
                };
            }).ToList();
        }

        public async Task<Dictionary<int, Dictionary<DateOnly, bool>>> GetEmployeeAvailability(int? employeeId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var endDate = today.AddDays(20);

            var allEmployees = await _context.Users.ToListAsync();
            var availability = new Dictionary<int, Dictionary<DateOnly, bool>>();

            foreach (var emp in allEmployees)
            {
                var days = new Dictionary<DateOnly, bool>();
                for (var d = today; d <= endDate; d = d.AddDays(1))
                    days[d] = true;
                availability[emp.userId] = days;
            }

            var query = _context.Bookings.AsQueryable();
            if (employeeId.HasValue)
                query = query.Where(b => b.Userid == employeeId.Value);

            var bookings = await query
                .Where(b => b.travelDate >= today && b.travelDate <= endDate && b.Status != Status.Canceled)
                .ToListAsync();

            foreach (var booking in bookings)
            {
                if (availability.ContainsKey(booking.Userid ?? 0) &&
                    availability[booking.Userid ?? 0].ContainsKey(booking.travelDate))
                    availability[booking.Userid ?? 0][booking.travelDate] = false;
            }

            if (employeeId.HasValue)
                return new Dictionary<int, Dictionary<DateOnly, bool>>
                    { { employeeId.Value, availability[employeeId.Value] } };

            return availability;
        }

        public async Task<List<Booking>> FilterUsersBookingsAsync(UserFilterDTO filterDTO)
        {
            var query = _context.Bookings.Where(b => b.Userid == filterDTO.userId);

            if (filterDTO.PerticularDate.HasValue) query = query.Where(b => b.travelDate == filterDTO.PerticularDate.Value);
            if (filterDTO.StartDate.HasValue) query = query.Where(b => b.travelDate >= filterDTO.StartDate.Value);
            if (filterDTO.EndDate.HasValue) query = query.Where(b => b.travelDate <= filterDTO.EndDate.Value);
            if (filterDTO.Status.HasValue) query = query.Where(b => b.Status == filterDTO.Status.Value);
            if (filterDTO.BookingType.HasValue) query = query.Where(b => b.BookingType == filterDTO.BookingType.Value);
            if (filterDTO.TravelTime.HasValue) query = query.Where(b => b.Traveltime == filterDTO.TravelTime.Value);

            return await query.ToListAsync();
        }

        public async Task<OvertimeLog> RequestOvertimeAsync(OvertimeRequestDTO request)
        {
            if (request.Hours <= 0) throw new ArgumentException("Minimum overtime must be 1 hour");

            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) throw new Exception("User not found");

            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null) throw new Exception("Booking not found");

            var overtime = new OvertimeLog
            {
                userId = request.UserId,
                hours = request.Hours,
                Description = request.Description,
                Date = request.Date,
                BookingId = request.BookingId,
                IsApproved = false
            };

            await _context.overtimeLogs.AddAsync(overtime);
            await _context.SaveChangesAsync();
            return overtime;
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(x => x.Email == email && x.Status);
        }

        public async Task UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        private async Task<Salary> AddSalary(decimal baseSalary, int userId)
        {
            var now = DateTime.Now;
            var salary = new Salary
            {
                BaseSalay = baseSalary,
                Deduction = 0,
                Overtimepay = 0,
                NetSalaey = baseSalary,
                Month = now.Month,
                Year = now.Year,
                userID = userId
            };
            await _context.salaries.AddAsync(salary);
            await _context.SaveChangesAsync();
            return salary;
        }
    }
}