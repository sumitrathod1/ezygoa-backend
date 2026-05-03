using TravelManagement.Core.Models;

namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IInquiryRepository
    {
        Task<List<EmailInquiry>> GetPendingInquiriesAsync();
        Task<EmailInquiry?> GetByIdAsync(int id);
        Task UpdateInquiryAsync(EmailInquiry inquiry);
        Task<List<Notification>> GetUnreadNotificationsAsync();
        Task<Notification?> GetNotificationByIdAsync(int id);
        Task MarkAllNotificationsAsReadAsync();
        Task<int> GetVehicleIdByTypeAsync(VechileType vehicleType, string? vehicleName);
        Task SaveChangesAsync();
    }
}