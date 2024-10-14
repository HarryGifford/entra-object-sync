using System.Text.Json.Serialization;
using Havok.Schema;
using Havok.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ZendeskApi_v2;

namespace Havok.Functions;

[Serializable]
public class HkGetAllEndUsersResult
{

    [JsonPropertyName("users")]
    public IList<ZendeskApi_v2.Models.Users.User> Users { get; set; } = [];

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class HkGetAllEndUsers(
    ILogger<HkGetAllEndUsers> logger,
    ZendeskService zendeskService)
{
    [Function(nameof(HkGetAllEndUsers))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkGetAllEndUsers)}.");

        var usersStore = new ZdUserStore(logger, zendeskService);
        usersStore.LoadAllEndUsers().Wait();
        var usersWithoutOrganization = usersStore.Users
            .Where(user => user.Role == "end-user" && user.Email != null && user.OrganizationId == null)
            .ToList();
        logger.LogInformation($"Found {usersWithoutOrganization.Count} users without an organization.");

        return new OkObjectResult(new HkGetAllEndUsersResult
        {
            Users = usersWithoutOrganization,
            Count = usersWithoutOrganization.Count
        });
    }
}
