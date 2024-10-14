using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZendeskApi_v2;

namespace Havok;

public class ZdOrganizationStore(ILogger Logger, ZendeskService ZendeskApi)
{
    public List<ZendeskApi_v2.Models.Organizations.Organization> Organizations { get; set; } = [];

    public async Task LoadAllOrganizations()
    {
        //Organizations = await ZendeskApi.GetAllOrganizations();
        // Write the organizations to a file.
        //var json = JsonSerializer.Serialize(Organizations);
        //await File.WriteAllTextAsync("organizations.json", json);
        if (!File.Exists("organizations.json"))
        {
            Organizations = await ZendeskApi.GetAllOrganizations();
            var json = JsonSerializer.Serialize(Organizations);
            await File.WriteAllTextAsync("organizations.json", json);
        }
        else
        {
            string text = await File.ReadAllTextAsync("organizations.json");
            var organizations = JsonSerializer.Deserialize<List<ZendeskApi_v2.Models.Organizations.Organization>>(text)
                ?? throw new Exception("Failed to deserialize organizations.");
            Organizations = organizations;
        }
        // Convert the organizations to a CSV file.
        var csv = new StringBuilder();
        csv.AppendLine("Id,Name,SalesforceId,DynamicsId");
        foreach (var organization in Organizations)
        {
            var customFields = organization.OrganizationFields;
            customFields.TryGetValue("havok_sfprojectid", out object? sfProjectIdObject);
            string sfProjectId = sfProjectIdObject?.ToString() ?? string.Empty;

            customFields.TryGetValue("dynamics_accountid_deprecated", out object? dynamicsAccountIdValue);
            string dynamicsAccountId = dynamicsAccountIdValue?.ToString() ?? string.Empty;

            string id = organization.Id.ToString() ?? string.Empty;
            string name = organization.Name ?? string.Empty;
            csv.AppendLine($"{id},\"{name}\",\"{sfProjectId}\",\"{dynamicsAccountId}\"");
        }
        await File.WriteAllTextAsync("organizations.csv", csv.ToString());
    }
}
