using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZendeskApi_v2;

namespace Havok;

public class ZdUserStore(ILogger Logger, ZendeskService ZendeskApi)
{
    public List<ZendeskApi_v2.Models.Users.User> Users { get; set; } = [];

    public async Task LoadAllEndUsers()
    {
        //Users = await ZendeskApi.GetAllUsers();
        // Write the users to a file.
        //var json = JsonSerializer.Serialize(Users);
        //await File.WriteAllTextAsync("users.json", json);
        if (!File.Exists("users.json"))
        {
            Users = await ZendeskApi.GetAllUsers();
            var json = JsonSerializer.Serialize(Users);
            await File.WriteAllTextAsync("users.json", json);
        }
        else
        {
            string text = await File.ReadAllTextAsync("users.json");
            var users = JsonSerializer.Deserialize<List<ZendeskApi_v2.Models.Users.User>>(text)
                ?? throw new Exception("Failed to deserialize users.");
            Users = users;
        }
        // Convert the users to a CSV file.
        var csv = new StringBuilder();
        csv.AppendLine("Id,Name,Email,SalesforceId,DynamicsId");
        foreach (var user in Users)
        {
            var customFields = user.CustomFields;
            customFields.TryGetValue("salesforce_contact_id", out object? sfContactIdObject);
            string sfContactId = sfContactIdObject?.ToString() ?? string.Empty;

            customFields.TryGetValue("dynamics_contact_id", out object? dynamicsContactIdValue);
            string dynamicsContactId = dynamicsContactIdValue?.ToString() ?? string.Empty;

            string id = user.Id.ToString() ?? string.Empty;
            string name = user.Name ?? string.Empty;
            string email = user.Email ?? string.Empty;
            csv.AppendLine($"{id},\"{name}\",\"{email}\",\"{sfContactId}\",\"{dynamicsContactId}\"");
        }
        await File.WriteAllTextAsync("users.csv", csv.ToString());
    }
}
