using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.BusinessLogicLayer.Services.Interface;
using TravelManagement.Core.DTOs;
using TravelManagement.Core.Models;

namespace TravelManagement.API.Controllers
{
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class UserController : ApiControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("save-token")]
        public async Task<IActionResult> SaveFcmToken(int userId, [FromBody] string fcmToken)
        {
            if (string.IsNullOrWhiteSpace(fcmToken))
                return ApiBadRequest("FCM token cannot be empty");

            await _userService.SaveFcmTokenAsync(userId, fcmToken);
            return ApiOk("FCM token saved successfully");
        }

        [HttpPost("authenticate")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Authenticate([FromBody] AuthDTO authDTO)
        {
            var (_, token) = await _userService.AuthenticateAsync(authDTO);
            return ApiOk(new { token }, "Login successful");
        }

        [HttpGet("getall-Users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return ApiOk(users);
        }

        [HttpGet("get-user")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            return user is null ? ApiNotFound($"User with ID {id} not found") : ApiOk(user);
        }

        [HttpPost("Register")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            var newUser = await _userService.RegisterAsync(user);
            return ApiCreated(newUser, "User registered successfully");
        }

        [HttpPost("changePassword")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDTO dto)
        {
            if (dto is null) return ApiBadRequest("Password data is required");
            await _userService.ChangePasswordAsync(dto);
            return ApiOk("Password changed successfully");
        }

        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO dto)
        {
            await _userService.ForgotPasswordAsync(dto);
            return ApiOk("OTP has been sent to your registered email");
        }

        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto)
        {
            await _userService.ResetPasswordAsync(dto);
            return ApiOk("Password reset successfully");
        }

        [HttpPut("Update/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDTO dto)
        {
            if (dto is null) return ApiBadRequest("User data is required");
            var updated = await _userService.UpdateUserProfileAsync(id, dto);
            return updated is null
                ? ApiNotFound($"User with ID {id} not found")
                : ApiOk(updated, "User updated successfully");
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            bool result = await _userService.DeleteUserAsync(id);
            return result
                ? ApiOk("User deactivated successfully")
                : ApiNotFound($"User with ID {id} not found");
        }

        [HttpPost("restore/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RestoreUser(int id)
        {
            bool result = await _userService.RestoreUserAsync(id);
            return result
                ? ApiOk("User restored successfully")
                : ApiNotFound($"User with ID {id} not found");
        }

        [HttpGet("getall-deleted")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllDeleted()
        {
            var users = await _userService.GetAllUsersIncludingDeletedAsync();
            var deleted = users.Where(u => u.IsDeleted).ToList();
            return ApiOk(deleted);
        }

        [HttpGet("available")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAvailableDrivers([FromQuery] DateOnly? date = null)
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.Today);
            var drivers = await _userService.GetAvailableDriversAsync(targetDate);
            return ApiOk(drivers);
        }

        [Authorize(Roles = "Employee")]
        [HttpGet("ViewBookings")]
        public async Task<IActionResult> EmpBookings(int id)
        {
            var bookings = await _userService.GetEmployeeBookingsAsync(id);
            if (bookings is null || !bookings.Any()) return ApiNoContent();
            return ApiOk(bookings);
        }

        [HttpGet("employee-availability")]
        public async Task<IActionResult> GetEmployeeAvailability(int? employeeId = null)
        {
            var availability = await _userService.GetEmployeeAvailabilityAsync(employeeId);
            if (availability is null || !availability.Any())
                return ApiNotFound("No availability data found");
            return ApiOk(availability);
        }

        [HttpGet("User-BookingFilter")]
        public async Task<IActionResult> GetFilteredBookings([FromQuery] UserFilterDTO userFilterDTO)
        {
            var result = await _userService.GetFilteredBookingsAsync(userFilterDTO);
            return ApiOk(result);
        }

        [HttpPost("RequestOvertime")]
        public async Task<IActionResult> RequestOvertime([FromBody] OvertimeRequestDTO request)
        {
            var overtime = await _userService.RequestOvertimeAsync(request);
            return ApiCreated(new
            {
                overtime.OvertimeID,
                overtime.hours,
                overtime.Date,
                overtime.BookingId
            }, "Overtime request submitted successfully");
        }
    }
}