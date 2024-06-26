using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class Opportunity : SObject {

    [JsonIgnore]
    public static string SObjectTypeName => "Opportunity";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    public string? Name { get; set; }

    [JsonProperty(nameof(Territory__c))]
    public string? Territory__c { get; set; }

    [JsonProperty(nameof(HavokVersion__c))]
    public string? HavokVersion__c { get; set; }

    [JsonProperty(nameof(Havok_AI__c))]
    public bool? Havok_AI__c { get; set; }

    [JsonProperty(nameof(Havok_Physics__c))]
    public bool? Havok_Physics__c { get; set; }

    [JsonProperty(nameof(Havok_Animation__c))]
    public bool? Havok_Animation__c { get; set; }

    [JsonProperty(nameof(Cloth__c))]
    public bool? Cloth__c { get; set; }
}

internal class OpportunityExpanded : Opportunity {
    [JsonProperty(nameof(OpportunityContactRoles))]
    public List<OpportunityContactRoleExpanded>? OpportunityContactRoles { get; set; }
}
