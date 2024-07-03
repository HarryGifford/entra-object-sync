using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;
using Havok.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class OpportunityContactRole : SObject {

    [JsonIgnore]
    public static string SObjectTypeName => "OpportunityContactRole";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(ContactId))]
    public string? ContactId { get; set; }

    [JsonProperty(nameof(OpportunityId))]
    public string? OpportunityId { get; set; }

    [JsonProperty(nameof(Role))]
    public string? Role { get; set; }

    [JsonProperty(nameof(Contact))]
    [Gettable(false)]
    public Contact? Contact { get; set; }

    [JsonProperty(nameof(Opportunity))]
    [Updateable(false), Createable(false), Gettable(false)]
    public Opportunity? Opportunity { get; set; }

    public ZendeskApi_v2.Models.Users.User? ToZendeskUser() => Contact?.ToZendeskUser();
}
