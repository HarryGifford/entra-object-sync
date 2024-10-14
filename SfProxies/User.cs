using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;
using Havok.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class User : SObject {

    [JsonIgnore]
    public static string SObjectTypeName => "User";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    public string? Name { get; set; }

    [JsonProperty(nameof(IsActive))]
    public bool? IsActive { get; set; }

    [JsonProperty(nameof(Email))]
    public string? Email { get; set; }

    [JsonProperty(nameof(Username))]
    public string? Username { get; set; }

    [JsonProperty(nameof(Alias))]
    public string? Alias { get; set; }

    [JsonProperty(nameof(FirstName))]
    public string? FirstName { get; set; }

    [JsonProperty(nameof(LastName))]
    public string? LastName { get; set; }

    [JsonProperty(nameof(Title))]
    public string? Title { get; set; }

    [JsonProperty(nameof(Phone))]
    public string? Phone { get; set; }

    [JsonProperty(nameof(Department))]
    public string? Department { get; set; }

    [JsonProperty(nameof(TimeZoneSidKey))]
    public string? TimeZoneSidKey { get; set; }

    [JsonProperty(nameof(LocaleSidKey))]
    public string? LocaleSidKey { get; set; }

    [JsonProperty(nameof(LanguageLocaleKey))]
    public string? LanguageLocaleKey { get; set; }

    [JsonProperty(nameof(EmailEncodingKey))]
    public string? EmailEncodingKey { get; set; }

    [JsonProperty(nameof(ProfileId))]
    public string? ProfileId { get; set; }

    [JsonProperty(nameof(FederationIdentifier))]
    public string? FederationIdentifier { get; set; }

    [JsonProperty(nameof(CommunityNickname))]
    public string? CommunityNickname { get; set; }

    [JsonProperty(nameof(Github_Username__c))]
    public string? Github_Username__c { get; set; }

    [JsonProperty(nameof(CrudOperation))]
    [Updateable(false), Createable(false), Gettable(false)]
    public string? CrudOperation { get; set; }

    public HkAuthEvents.ZdProxies.User ToZendeskUser() => new()
    {
        Id = FederationIdentifier != null ? long.Parse(FederationIdentifier) : null,
        Name = Name,
        Email = Email,
        Phone = Phone,
        // Role = "agent",
        Verified = true,
        Active = IsActive,
        Locale = LocaleSidKey?.Replace("_", "-"),
        //TimeZone = TimeZoneSidKey,
        CustomFields = new Dictionary<string, object?>()
        {
            { "salesforce_contact_id", Id },
            { "github_username", Github_Username__c }
        }
    };
}
