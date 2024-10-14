using Newtonsoft.Json;

namespace Havok.HkAuthEvents.ZdProxies;

[JsonObject]
public class OrganizationFieldOption
{
    [JsonProperty("id", NullValueHandling = NullValueHandling.Include, DefaultValueHandling = DefaultValueHandling.Populate)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
    public string? Id { get; set; }
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }
    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public string? Value { get; set; }
}

[JsonObject]
public class OrganizationField
{
    [JsonProperty("key", NullValueHandling = NullValueHandling.Ignore)]
    public required string Key { get; set; }
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }
    [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
    public string? Title { get; set; }
    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }
    [JsonProperty("custom_field_options", NullValueHandling = NullValueHandling.Ignore)]
    public OrganizationFieldOption[]? CustomFieldOptions { get; set; }
}

[JsonObject]
public class OrganizationFieldResponse
{
    [JsonProperty("organization_field", NullValueHandling = NullValueHandling.Ignore)]
    public OrganizationField? OrganizationField { get; set; }
}