using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class Account : SObject {

    [JsonIgnore]
    public static string SObjectTypeName => "Account";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    public string? Name { get; set; }

    [JsonProperty(nameof(AnnualRevenue))]
    public double? AnnualRevenue { get; set; }

    [JsonProperty(nameof(ClientPriority__c))]
    public string? ClientPriority__c { get; set; }

    [JsonProperty(nameof(NumberOfEmployees))]
    public int? NumberOfEmployees { get; set; }

    [JsonProperty(nameof(Phone))]
    public string? Phone { get; set; }

    [JsonProperty(nameof(Website))]
    public string? Website { get; set; }

    [JsonProperty(nameof(Approved_Platform_s__c))]
    public string? Approved_Platform_s__c { get; set; }

    [JsonProperty(nameof(Developer_Type__c))]
    public string? Developer_Type__c { get; set; }

    [JsonProperty(nameof(Account_Name_Simplified__c))]
    public string? Account_Name_Simplified__c { get; set; }

    [JsonProperty(nameof(Account_Display_Name__c))]
    public string? Account_Display_Name__c { get; set; }

    [JsonProperty(nameof(Account_Status_BW_del__c))]
    public string? Account_Status_BW_del__c { get; set; }
}

internal class AccountExpanded : Account {
    [JsonProperty(nameof(Contacts))]
    public List<Contact>? Contacts { get; set; }

    [JsonProperty(nameof(Projects__r))]
    public List<Project>? Projects__r { get; set; }
}
