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
public class HkUpdateOrganizationMembershipsResult {

    [JsonPropertyName("organizations")]
    public IList<ZendeskApi_v2.Models.Organizations.Organization> Organizations { get; set; } = [];
}

public class HkUpdateOrganizationMemberships(
    ILogger<HkUpdateOrganizationMemberships> logger,
    ZendeskService zendeskService)
{
    [Function(nameof(HkUpdateOrganizationMemberships))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkUpdateOrganizationMemberships)}.");

        var HkZendeskService = new HkZendeskService(logger, zendeskService);

        //var organizationsStore = new ZdOrganizationStore(logger, zendeskService);
        //organizationsStore.LoadAllOrganizations().Wait();
        // Load CSV file with membership data.
        var csvdata = File.ReadAllText("C:\\Users\\hegi\\Downloads\\users_zendesk_dynamics_accounts.csv");
        var csvData = csvdata.Split("\n").Skip(1).Select(x => {
            var data = x.Split(",");
            if (data.Length < 2)
            {
                return null;
            }
            var organizationMembership = new ZendeskApi_v2.Models.Organizations.OrganizationMembership
            {
                UserId = string.IsNullOrWhiteSpace(data[0]) ? null : long.Parse(data[0]),
                OrganizationId = string.IsNullOrWhiteSpace(data[1]) ? null : long.Parse(data[1])
            };
            return organizationMembership;
        }).Where(x => x.UserId != null && x.OrganizationId != null);

        string startIdxStr = req.Query["startIdx"]!;
        int idx = string.IsNullOrWhiteSpace(startIdxStr) ? 0 : int.Parse(startIdxStr);
        foreach (var chunk in csvData.Chunk(100))
        {
            logger.LogInformation($"Processing chunk {++idx} of {csvData.Count()}...");
            var response = zendeskService.Organizations.CreateManyOrganizationMemberships(chunk);
            response = HkZendeskService.WaitForJobCompletion(response);
        }

        return new OkObjectResult(new HkUpdateOrganizationMembershipsResult { });
    }
}
