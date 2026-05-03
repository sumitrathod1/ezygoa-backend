using Microsoft.AspNetCore.SignalR;

namespace TravelManagement.API.Hubs
{
    public class NotificationHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"SignalR connected: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }
    }
}