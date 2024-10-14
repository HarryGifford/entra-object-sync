using Newtonsoft.Json;
using NetCoreForce.Client.Models;
using NetCoreForce.Client.Attributes;
using ZendeskApi_v2.Models.Organizations;
using Havok.Attributes;

namespace Havok.HkProjectCreate.SfProxies;

[JsonObject]
internal class Project : SObject {

    [JsonIgnore]
    public static string SObjectTypeName => "Project__c";

    [JsonProperty(nameof(Id))]
    [Updateable(false), Createable(false)]
    public required string Id { get; set; }

    [JsonProperty(nameof(Name))]
    [Updateable(true), Createable(true)]
    public string? Name { get; set; }

    [JsonProperty(nameof(Primary_Account_Manager__c))]
    [Updateable(true), Createable(true)]
    public string? Primary_Account_Manager__c { get; set; }

    [JsonProperty(nameof(Secondary_Account_Manager__c))]
    [Updateable(true), Createable(true)]
    public string? Secondary_Account_Manager__c { get; set; }

    [JsonProperty(nameof(Account_Manager_Primary__c))]
    [Updateable(true), Createable(true)]
    public string? Account_Manager_Primary__c { get; set; }

    [JsonProperty(nameof(Account_Manager_Secondary__c))]
    [Updateable(true), Createable(true)]
    public string? Account_Manager_Secondary__c { get; set; }

    [JsonProperty(nameof(Account_Manager_Tertiary__c))]
    [Updateable(true), Createable(true)]
    public string? Account_Manager_Tertiary__c { get; set; }

    [JsonProperty(nameof(PrimaryOpportunity__c))]
    [Updateable(true), Createable(true)]
    public string? PrimaryOpportunity__c { get; set; }

    [JsonProperty(nameof(Project_Status__c))]
    [Updateable(true), Createable(true)]
    public string? Project_Status__c { get; set; }

    [JsonProperty(nameof(Support_Management_Status__c))]
    [Updateable(true), Createable(true)]
    public string? Support_Management_Status__c { get; set; }

    [JsonProperty(nameof(Development_Platform_s__c))]
    [Updateable(true), Createable(true)]
    public string? Development_Platform_s__c { get; set; }

    [JsonProperty(nameof(Developer__c))]
    [Updateable(true), Createable(true)]
    public string? Developer__c { get; set; }

    [JsonProperty(nameof(Publisher__c))]
    [Updateable(true), Createable(true)]
    public string? Publisher__c { get; set; }

    [JsonProperty(nameof(Products__c))]
    [Updateable(true), Createable(true)]
    public string? Products__c { get; set; }

    [JsonProperty(nameof(Zendesk_organization_id__c))]
    [Updateable(true), Createable(true)]
    public string? Zendesk_organization_id__c { get; set; }

    [JsonProperty(nameof(UE4_Integration_GitHub_Access__c))]
    [Updateable(true), Createable(true)]
    public bool? UE4_Integration_GitHub_Access__c { get; set; }

    [JsonProperty(nameof(Physics_Engine__c))]
    [Updateable(true), Createable(true)]
    public string? Physics_Engine__c { get; set; }

    protected string SlaFromProject() => Support_Management_Status__c switch
    {
        "With FAE - Evaluation" => "havok_servicelevelagreement_Evaluator",
        "With DevRel - Active AM" => "havok_servicelevelagreement_Client",
        _ => "havok_servicelevelagreement_No_Access",
    };

    [JsonProperty(nameof(Account_Manager_Primary__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public User? Account_Manager_Primary__r { get; set; }

    [JsonProperty(nameof(Account_Manager_Secondary__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public User? Account_Manager_Secondary__r { get; set; }

    [JsonProperty(nameof(Account_Manager_Tertiary__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public User? Account_Manager_Tertiary__r { get; set; }

    [JsonProperty(nameof(PrimaryOpportunity__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public Opportunity? PrimaryOpportunity__r { get; set; }

    [JsonProperty(nameof(Publisher__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public Account? Publisher__r { get; set; }

    [JsonProperty(nameof(Developer__r))]
    [Updateable(false), Createable(false), Gettable(false)]
    public Account? Developer__r { get; set; }

    public Organization ToZendeskOrganization()
    {
        var name = NameFromProject();
        var accountInactive = Publisher__r?.Account_Status_BW_del__c == "Inactive";
        var havokVersion = PrimaryOpportunity__r?.HavokVersion__c;
        var territory = PrimaryOpportunity__r?.Territory__c;
        var sfAccountId = Developer__c;
        var sfProjectId = Id;
        var sfOpportunityId = PrimaryOpportunity__c;
        var zdId = Zendesk_organization_id__c;
        var sla = SlaFromProject();
        var tags = new List<string>();
        var productTags = new List<string>();
        TagsFromProjectProducts(productTags);
        var orgEngineTag = UE4_Integration_GitHub_Access__c != null ? "org_engine_unreal" : "org_engine_custom";
        var clientPriority = PriorityFromProject();
        var customerType = sla == "havok_servicelevelagreement_Evaluator" ? "havok_type_Evaluation" : "havok_type_Support";

        const long devRelNaId = 23741505645204;
        const long devRelEuropeId = 23744675895956;
        const long devRelJapanId = 23744675873940;
        const long faeId = 23744711314196;

        long? orgGroup = territory switch
        {
            "NANW" => devRelNaId,
            "NANE" => devRelNaId,
            "NASW" => devRelNaId,
            "EMEA" => devRelEuropeId,
            "ASIA" => devRelJapanId,
            "Japan Office" => devRelJapanId,
            "Korea Office" => devRelJapanId,
            _ => null
        };

        string? territoryCode = territory switch
        {
            "NANW" => "territorycode_NANW",
            "NANE" => "territorycode_NANE",
            "NASW" => "territorycode_NASW",
            "EMEA" => "territorycode_EMEA",
            "ASIA" => "territorycode_ASIA",
            "Japan Office" => "territorycode_ASIA",
            "Korea Office" => "territorycode_Korea_Office",
            _ => null
        };

        orgGroup = sla == "havok_servicelevelagreement_Evaluator" ? faeId : orgGroup;

        string? primaryAccountManagerLookupValue = Account_Manager_Primary__r?.FederationIdentifier != null
            ? $"external_id:{Account_Manager_Primary__r.FederationIdentifier}"
            : null;

        string? secondaryAccountManagerLookupValue = Account_Manager_Secondary__r?.FederationIdentifier != null
            ? $"external_id:{Account_Manager_Secondary__r.FederationIdentifier}"
            : null;

        string? tertiaryAccountManagerLookupValue = Account_Manager_Tertiary__r?.FederationIdentifier != null
            ? $"external_id:{Account_Manager_Tertiary__r.FederationIdentifier}"
            : null;

        var organization = new Organization
        {
            Id = zdId != null ? long.Parse(zdId) : null,
            Name = name,
            SharedTickets = true,
            SharedComments = true,
            GroupId = orgGroup,
            Tags = tags,
            OrganizationFields = new Dictionary<string, object?> {
                { "territory_code", territoryCode },
                { "havok_sfaccountid", sfAccountId },
                { "havok_sfprojectid", sfProjectId },
                { "havok_sfopportunityid", sfOpportunityId! },
                { "havok_servicelevelagreement", sla },
                { "org_disabled", accountInactive },
                { "havok_version", havokVersion ?? null },
                { "havok_clientpriority_deprecated", clientPriority ?? null },
                { "customer_type", customerType },
                { "primary_account_manager", primaryAccountManagerLookupValue },
                { "secondary_account_manager", secondaryAccountManagerLookupValue },
                { "tertiary_account_manager", tertiaryAccountManagerLookupValue },
                { "havok_products", productTags },
                { "org_engine", orgEngineTag }
            }
        };
        return organization;
    }

    protected string? PriorityFromProject() => Developer__r?.ClientPriority__c switch
    {
        "p0" => "havok_clientpriority_High",
        "p1" => "havok_clientpriority_Medium",
        "p2" => "havok_clientpriority_Slim",
        "p3" => "havok_clientpriority_Zero",
        _ => null
    };

    /// <summary>
    /// Constructs the name of the organization from the project, publisher, and developer.
    /// </summary>
    protected string NameFromProject()
    {
        var publisherName = Publisher__r?.Account_Name_Simplified__c ?? Publisher__r?.Account_Display_Name__c;
        var developerName = Developer__r?.Account_Name_Simplified__c ?? Developer__r?.Account_Display_Name__c;
        var projectName = Name;

        // Only have the project name -- check that it's not empty.
        if (string.IsNullOrWhiteSpace(publisherName) && string.IsNullOrWhiteSpace(developerName))
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new InvalidOperationException("Project must have a name, publisher, or developer.");
            }
            return projectName;
        }

        // Only have the publisher name.
        if (publisherName == developerName || string.IsNullOrWhiteSpace(developerName))
        {
            return $"{projectName} : {publisherName}";
        }
        // Merge names like "Ubisoft : Ubisoft Blah" into "Ubisoft : Blah".
        else if (string.IsNullOrWhiteSpace(publisherName) || developerName.StartsWith(publisherName))
        {
            return $"{projectName} : {developerName}";
        }
        else
        {
            return $"{projectName} : {developerName} : {publisherName}";
        }
    }

    protected void TagsFromProjectProducts(IList<string> tags)
    {
        var products = Products__c != null ? Products__c.Split(";") : [];
        if (products.Contains("AI") || products.Contains("Navigation"))
        {
            tags.Add("product_navigation");
        }
        
        if (products.Contains("Physics"))
        {
            tags.Add("product_physics");
        }

        if (products.Contains("Animation"))
        {
            tags.Add("product_animation");
        }

        if (products.Contains("Cloth"))
        {
            tags.Add("product_cloth");
        }

        if (products.Contains("Destruction"))
        {
            tags.Add("product_destruction");
        }

        if (products.Contains("Script"))
        {
            tags.Add("product_script");
        }
    }
}
