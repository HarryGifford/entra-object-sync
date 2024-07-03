using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;
using System.Text.Json.Serialization;
using Havok.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[Serializable]
[JsonObject]
internal class Opportunity : SObject {

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public static string SObjectTypeName => "Opportunity";

    [JsonProperty(nameof(Id))]
    [JsonPropertyName(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    [JsonPropertyName(nameof(Name))]
    public string? Name { get; set; }

    [JsonProperty(nameof(Territory__c))]
    [JsonPropertyName(nameof(Territory__c))]
    public string? Territory__c { get; set; }

    [JsonProperty(nameof(HavokVersion__c))]
    [JsonPropertyName(nameof(HavokVersion__c))]
    public string? HavokVersion__c { get; set; }

    [JsonProperty(nameof(Havok_AI__c))]
    [JsonPropertyName(nameof(Havok_AI__c))]
    public bool? Havok_AI__c { get; set; }

    [JsonProperty(nameof(Havok_Physics__c))]
    [JsonPropertyName(nameof(Havok_Physics__c))]
    public bool? Havok_Physics__c { get; set; }

    [JsonProperty(nameof(Havok_Animation__c))]
    [JsonPropertyName(nameof(Havok_Animation__c))]
    public bool? Havok_Animation__c { get; set; }

    [JsonProperty(nameof(Cloth__c))]
    [JsonPropertyName(nameof(Cloth__c))]
    public bool? Cloth__c { get; set; }

    [JsonProperty(nameof(OpportunityContactRoles))]
    [Updateable(false), Createable(false), Gettable(false)]
    public List<OpportunityContactRole>? OpportunityContactRoles { get; set; }
}

[JsonSerializable(typeof(Opportunity))]
internal partial class OpportunityContext : JsonSerializerContext { }
