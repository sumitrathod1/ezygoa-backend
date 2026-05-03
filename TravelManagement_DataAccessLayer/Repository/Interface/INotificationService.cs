namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface INotificationService
    {
        Task CreateAsync(string message, int? userId = null);
        Task SendPushNotificationAsync(string title, string body);
    }
}