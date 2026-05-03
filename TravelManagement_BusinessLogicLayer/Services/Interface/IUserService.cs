using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface IUserService
    {
        Task SaveFcmTokenAsync(int userId, string fcmToken);
        Task<(User user, string token)> AuthenticateAsync(AuthDTO dto);
        Task<List<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int id);
        Task<User> RegisterAsync(User user);
        Task ChangePasswordAsync(ChangePasswordDTO dto);
        Task ForgotPasswordAsync(ForgotPasswordRequestDTO dto);
        Task ResetPasswordAsync(ResetPasswordRequestDto dto);
        Task<User?> UpdateUserProfileAsync(int id, UpdateUserDTO dto);
        Task<bool> DeleteUserAsync(int id);
        Task<bool> RestoreUserAsync(int id);
        Task<List<User>> GetAllUsersIncludingDeletedAsync();
        Task<List<User>> GetAvailableDriversAsync(DateOnly date);
        Task<IEnumerable<object>> GetEmployeeBookingsAsync(int userId);
        Task<Dictionary<int, Dictionary<DateOnly, bool>>> GetEmployeeAvailabilityAsync(int? employeeId);
        Task<List<Booking>> GetFilteredBookingsAsync(UserFilterDTO dto);
        Task<OvertimeLog> RequestOvertimeAsync(OvertimeRequestDTO dto);
    }
}