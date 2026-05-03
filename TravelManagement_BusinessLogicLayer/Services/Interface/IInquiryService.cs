using TravelManagement.Core.Models;

namespace TravelManagement.BusinessLogicLayer.Services.Interface
{
    public interface IInquiryService
    {
        Task<List<EmailInquiry>> GetAllInquiriesAsync();
        Task<object> ConfirmInquiryAsync(int id);
        Task RejectInquiryAsync(int id);
        Task<List<Notification>> GetNotificationsAsync();
        Task<Notification?> MarkAsReadAsync(int id);
        Task<int> MarkAllAsReadAsync();
    }
}