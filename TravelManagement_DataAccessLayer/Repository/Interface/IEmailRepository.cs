namespace TravelManagement.DataAccessLayer.Repository.Interface
{
    public interface IEmailRepository
    {
        Task SendAsync(string to, string subject, string body);
    }
}