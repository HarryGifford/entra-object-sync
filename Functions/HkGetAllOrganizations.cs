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
public class HkGetAllOrganizationsResult {

    [JsonPropertyName("organizations")]
    public IList<ZendeskApi_v2.Models.Organizations.Organization> Organizations { get; set; } = [];
}

public class HkGetAllOrganizations(
    ILogger<HkGetAllOrganizations> logger,
    ZendeskService zendeskService)
{
    [Function(nameof(HkGetAllOrganizations))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkGetAllOrganizations)}.");

        var organizationsStore = new ZdOrganizationStore(logger, zendeskService);
        organizationsStore.LoadAllOrganizations().Wait();
        return new OkObjectResult(new HkGetAllOrganizationsResult { Organizations = organizationsStore.Organizations });
    }
}
