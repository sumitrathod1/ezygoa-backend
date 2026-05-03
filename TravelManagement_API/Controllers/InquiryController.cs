using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TravelManagement.BusinessLogicLayer.Services.Interface;

namespace TravelManagement.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [EnableRateLimiting("api")]
    public class InquiryController : ApiControllerBase
    {
        private readonly IInquiryService _inquiryService;

        public InquiryController(IInquiryService inquiryService)
        {
            _inquiryService = inquiryService;
        }

        [HttpGet("GetAllEnqueries")]
        public async Task<IActionResult> GetAllEnqueries()
        {
            var inquiries = await _inquiryService.GetAllInquiriesAsync();
            return ApiOk(inquiries);
        }

        [HttpPost("confirm/{id}")]
        public async Task<IActionResult> Confirm(int id)
        {
            var booking = await _inquiryService.ConfirmInquiryAsync(id);
            return ApiCreated(booking, "Inquiry confirmed and booking created");
        }

        [HttpPost("reject/{id}")]
        public async Task<IActionResult> Reject(int id)
        {
            await _inquiryService.RejectInquiryAsync(id);
            return ApiOk("Inquiry rejected successfully");
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _inquiryService.GetNotificationsAsync();
            return ApiOk(notifications);
        }

        [HttpPut("notifications/mark-read/{id}")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _inquiryService.MarkAsReadAsync(id);
            return notification is null
                ? ApiNotFound($"Notification with ID {id} not found")
                : ApiOk(notification, "Notification marked as read");
        }

        [HttpPut("notifications/mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var count = await _inquiryService.MarkAllAsReadAsync();
            return ApiOk($"{count} notifications marked as read");
        }
    }
}