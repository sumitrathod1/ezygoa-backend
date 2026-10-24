namespace TravelManagement.API.Hubs
{
    /// <summary>SignalR group names. Per organisation, so one org's events never reach another.</summary>
    public static class HubGroups
    {
        public static string Org(int orgId) => $"org-{orgId}";
        public static string OrgRole(int orgId, string role) => $"org-{orgId}-{role}";
    }
}
