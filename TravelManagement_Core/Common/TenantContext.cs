using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TravelManagement.Core.Common
{
    /// <summary>
    /// Scoped service that exposes the current request's tenant (org) and user identity.
    /// OrgId = 0 means "no tenant" — used by SuperAdmin or background services.
    /// </summary>
    public class TenantContext
    {
        /// <summary>Organisation ID from the JWT "orgId" claim. 0 = SuperAdmin / no org.</summary>
        public int OrgId { get; }

        /// <summary>User ID from the JWT NameIdentifier claim.</summary>
        public int UserId { get; }

        /// <summary>Role string from the JWT Role claim.</summary>
        public string Role { get; }

        /// <summary>True when the current user is a SuperAdmin (OrgId == 0 and Role == SuperAdmin).</summary>
        public bool IsSuperAdmin => Role == "SuperAdmin";

        /// <summary>
        /// True when we should apply an org filter.
        /// Returns false for SuperAdmin and background tasks (OrgId == 0).
        /// </summary>
        public bool ShouldFilter => OrgId > 0 && !IsSuperAdmin;

        public TenantContext(IHttpContextAccessor httpContextAccessor)
        {
            var claims = httpContextAccessor.HttpContext?.User;
            if (claims?.Identity?.IsAuthenticated == true)
            {
                OrgId  = int.TryParse(claims.FindFirst("orgId")?.Value, out var o) ? o : 0;
                UserId = int.TryParse(
                    claims.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u) ? u : 0;
                Role   = claims.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            }
        }
    }
}
