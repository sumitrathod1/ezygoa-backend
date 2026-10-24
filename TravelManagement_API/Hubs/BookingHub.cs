using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace TravelManagement.API.Hubs
{
    [Authorize]
    public class BookingHub : Hub
    {
        // Connections join "org-{id}-{role}" so a booking change only refreshes screens of
        // the same organisation (the plain role group made every org's admins refetch).
        private (string? Role, int Org) Who()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            int.TryParse(Context.User?.FindFirst("orgId")?.Value, out var org);
            return (role, org);
        }

        public override async Task OnConnectedAsync()
        {
            var (role, org) = Who();
            if (!string.IsNullOrEmpty(role))
                await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.OrgRole(org, role));
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var (role, org) = Who();
            if (!string.IsNullOrEmpty(role))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.OrgRole(org, role));
            await base.OnDisconnectedAsync(exception);
        }
    }
}
