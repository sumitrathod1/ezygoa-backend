using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IUserRepository
    {
        Task<User?> FindByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int id);
        Task<List<User>> GetAllActiveUsersAsync();
        Task SaveFcmTokenAsync(int userId, string fcmToken);
        Task<User> NewUser(User user);
        Task<bool> DeleteUser(int id);
        Task<IEnumerable<object>> GetBookingsWithPaymentsByUserIdAsync(int userId);
        Task<Dictionary<int, Dictionary<DateOnly, bool>>> GetEmployeeAvailability(int? employeeId = null);
        Task<List<Booking>> FilterUsersBookingsAsync(UserFilterDTO userFilterDTO);
        Task<OvertimeLog> RequestOvertimeAsync(OvertimeRequestDTO overtimeRequestDTO);
        Task<User?> GetUserByEmailAsync(string email);
        Task UpdateUserAsync(User user);
        int FindBookingId(int id, DateOnly selectedDate, Status bookingStatus);

        // Profile update
        Task<User?> UpdateUserProfileAsync(int id, UpdateUserDTO dto);

        // Soft delete / restore
        Task<bool> RestoreUser(int id);
        Task<List<User>> GetAllUsersIncludingDeletedAsync();
        Task<List<User>> GetAvailableDriversAsync(DateOnly date);
    }
}