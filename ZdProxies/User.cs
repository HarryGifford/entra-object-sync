using Newtonsoft.Json;

namespace Havok.HkAuthEvents.ZdProxies;

public class User : ZendeskApi_v2.Models.Users.User
{
    /// <summary>
    /// The user's locale. A BCP-47 compliant tag for the locale. If
    /// both "locale" and "locale_id" are present on create or update,
    /// "locale_id" is ignored and only "locale" is used.
    /// </summary>
    [JsonProperty("locale")]
    public string? Locale { get; set; }
}
