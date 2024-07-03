using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;
using Havok.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class Contact : SObject, IHkUser {
    [JsonIgnore]
    public static string SObjectTypeName => "Contact";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    public string? Name { get; set; }

    [JsonProperty(nameof(FirstName))]
    public string? FirstName { get; set; }

    [JsonProperty(nameof(LastName))]
    public string? LastName { get; set; }

    [JsonProperty(nameof(Email))]
    public string? Email { get; set; }

    [JsonProperty(nameof(Phone))]
    public string? Phone { get; set; }

    [JsonProperty(nameof(AccountId))]
    public string? AccountId { get; set; }

    [JsonProperty("Zendesk_user_id__c")]
    public string? ZendeskId { get; set; }

    [JsonProperty(nameof(GitHub_Username__c))]
    public string? GitHub_Username__c { get; set; }

    [JsonProperty(nameof(HasLeft__c))]
    public bool? HasLeft__c { get; set; }

    [JsonProperty(nameof(MailingCity))]
    public string? MailingCity { get; set; }

    [JsonProperty(nameof(MailingCountryCode))]
    public string? MailingCountryCode { get; set; }

    [JsonProperty(nameof(Title))]
    public string? Title { get; set; }

    [JsonProperty(nameof(Department))]
    public string? Department { get; set; }

    [JsonProperty(nameof(CrudOperation))]
    [Updateable(false), Createable(false), Gettable(false)]
    public string? CrudOperation { get; set; }

    public HkAuthEvents.ZdProxies.User ToZendeskUser() => new()
    {
        Id = ZendeskId != null ? long.Parse(ZendeskId) : null,
        Name = Name,
        Email = Email,
        Verified = true,
        Phone = Phone,
        Suspended = HasLeft__c ?? false,
        Role = "end-user",
        TicketRestriction = "organization",
        CustomFields = new Dictionary<string, object?>()
        {
            { "salesforce_contact_id", Id },
            { "github_username", GitHub_Username__c },
            { "usage_location", MailingCountryCode != null ? $"usage_location_{MailingCountryCode}" : null },
            { "title", Title },
            { "department", Department },
            { "city", MailingCity }
        }
    };
}
