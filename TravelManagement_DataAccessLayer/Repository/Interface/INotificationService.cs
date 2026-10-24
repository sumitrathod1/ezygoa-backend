namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface INotificationService
    {
        /// <summary>Store a notification for <paramref name="orgId"/> and push it live to that org only.</summary>
        Task CreateAsync(string message, int orgId, int? userId = null);

        /// <summary>Phone push to the admins of <paramref name="orgId"/> (not drivers, not other orgs).</summary>
        Task SendPushNotificationAsync(string title, string body, int orgId);
    }
}
