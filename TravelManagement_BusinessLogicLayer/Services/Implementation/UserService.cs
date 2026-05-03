using Microsoft.Extensions.Configuration;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.Common;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;
using TravelManagement.DataAccessLayer.Repository.Interface;

namespace TravelManagement.BusinessLogicLayer.Services.Implementation
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IEmailRepository _emailRepo;
        private readonly IConfiguration _config;

        public UserService(IUserRepository userRepo, IEmailRepository emailRepo, IConfiguration config)
        {
            _userRepo = userRepo;
            _emailRepo = emailRepo;
            _config = config;
        }

        public Task SaveFcmTokenAsync(int userId, string fcmToken) =>
            _userRepo.SaveFcmTokenAsync(userId, fcmToken);

        public async Task<(User user, string token)> AuthenticateAsync(AuthDTO dto)
        {
            var user = await _userRepo.FindByUsernameAsync(dto.userName)
                ?? throw new UnauthorizedAccessException("Invalid credentials");

            if (!user.Status)
                throw new UnauthorizedAccessException("User account is deactivated. Contact admin.");

            if (!PasswordHasher.VerifyPassword(dto.Password, user.Password))
                throw new UnauthorizedAccessException("Invalid credentials");

            var token = new JwtService(_config).GenerateToken(user);
            return (user, token);
        }

        public Task<List<User>> GetAllUsersAsync() => _userRepo.GetAllActiveUsersAsync();

        public Task<User?> GetUserByIdAsync(int id) => _userRepo.GetByIdAsync(id);

        public Task<User> RegisterAsync(User user) => _userRepo.NewUser(user);

        public async Task ChangePasswordAsync(ChangePasswordDTO dto)
        {
            var user = await _userRepo.FindByUsernameAsync(dto.UserName)
                ?? throw new KeyNotFoundException("User Not Found!");

            if (!PasswordHasher.VerifyPassword(dto.OldPassword, user.Password))
                throw new InvalidOperationException("Password is Incorrect");

            user.Password = PasswordHasher.HashPassword(dto.NewPassword);
            await _userRepo.UpdateUserAsync(user);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDTO dto)
        {
            var user = await _userRepo.GetUserByEmailAsync(dto.Email)
                ?? throw new KeyNotFoundException("User not found with this email");

            var otp = OtpHelper.GenerateOtp();
            user.ResetPasswordtoken = OtpHelper.HashOtp(otp);
            user.RestPasswordExpiry = DateTime.UtcNow.AddMinutes(10);
            await _userRepo.UpdateUserAsync(user);

            await _emailRepo.SendAsync(user.Email!, "Password Reset OTP",
                $"Your OTP is {otp}. Valid for 10 minutes.");
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto dto)
        {
            var user = await _userRepo.GetUserByEmailAsync(dto.Email)
                ?? throw new KeyNotFoundException("Invalid request");

            if (user.RestPasswordExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("OTP expired");

            if (user.ResetPasswordtoken != OtpHelper.HashOtp(dto.Otp))
                throw new InvalidOperationException("Invalid OTP");

            user.Password = PasswordHasher.HashPassword(dto.NewPassword);
            user.ResetPasswordtoken = null;
            await _userRepo.UpdateUserAsync(user);
        }

        public Task<User?> UpdateUserProfileAsync(int id, UpdateUserDTO dto) =>
            _userRepo.UpdateUserProfileAsync(id, dto);

        public Task<bool> DeleteUserAsync(int id) => _userRepo.DeleteUser(id);
        public Task<bool> RestoreUserAsync(int id) => _userRepo.RestoreUser(id);
        public Task<List<User>> GetAllUsersIncludingDeletedAsync() => _userRepo.GetAllUsersIncludingDeletedAsync();
        public Task<List<User>> GetAvailableDriversAsync(DateOnly date) => _userRepo.GetAvailableDriversAsync(date);

        public Task<IEnumerable<object>> GetEmployeeBookingsAsync(int userId) =>
            _userRepo.GetBookingsWithPaymentsByUserIdAsync(userId);

        public Task<Dictionary<int, Dictionary<DateOnly, bool>>> GetEmployeeAvailabilityAsync(int? employeeId) =>
            _userRepo.GetEmployeeAvailability(employeeId);

        public Task<List<Booking>> GetFilteredBookingsAsync(UserFilterDTO dto) =>
            _userRepo.FilterUsersBookingsAsync(dto);

        public Task<OvertimeLog> RequestOvertimeAsync(OvertimeRequestDTO dto) =>
            _userRepo.RequestOvertimeAsync(dto);
    }
}