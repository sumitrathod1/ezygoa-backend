using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TravelManagement.API.Hubs
{
    /// <summary>Enquiry / notification pushes for an organisation's admins.</summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            if (int.TryParse(Context.User?.FindFirst("orgId")?.Value, out var org) && org > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Org(org));
            await base.OnConnectedAsync();
        }
    }
}
