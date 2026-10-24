using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using TravelManagement.Core.Models;

namespace TravelManagement.API.Infrastructure
{
    /// <summary>
    /// Keeps secrets out of every API response without breaking request binding.
    /// User entities are returned directly by several endpoints (and nested inside
    /// bookings), so without this the BCrypt hash and the live password-reset
    /// token were serialised to clients. [JsonIgnore] is not usable because
    /// Register binds a User from the request body and needs Password.
    /// This only suppresses writing — deserialising these fields still works.
    /// </summary>
    public static class SensitiveJsonFields
    {
        private static readonly Dictionary<Type, HashSet<string>> Hidden = new()
        {
            [typeof(User)] = new() { nameof(User.Password), nameof(User.ResetPasswordtoken), nameof(User.RestPasswordExpiry), nameof(User.FcmToken) },
        };

        public static void Hide(JsonTypeInfo typeInfo)
        {
            if (!Hidden.TryGetValue(typeInfo.Type, out var names)) return;

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.AttributeProvider is MemberInfo member && names.Contains(member.Name))
                    prop.ShouldSerialize = static (_, _) => false;
            }
        }
    }
}
