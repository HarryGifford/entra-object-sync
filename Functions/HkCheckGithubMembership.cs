using System.Text.Json.Serialization;
using Havok.Schema;
using Havok.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Havok.Functions;

[Serializable]
public class HkCheckGitHubMembershipResult {

    [JsonPropertyName("isMember")]
    public bool IsMember { get; set; }
}

public class HkCheckGitHubMembership(
    ILogger<HkCheckGitHubMembership> logger,
    GithubService githubService)
{
    [Function(nameof(HkCheckGitHubMembership))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkCheckGitHubMembership)}.");
        
        var owner = req.Query["owner"];
        logger.LogInformation(owner);

        var username = req.Query["username"];
        logger.LogInformation(username);

        var repo = req.Query["repo"];
        logger.LogInformation(repo);

        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(repo))
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Please provide owner, username, and repo."
            });
        }


        try {
            var isMember = githubService.IsMemberOfRepo(owner!, repo!, username!);
            return new OkObjectResult(new HkCheckGitHubMembershipResult { IsMember = isMember });
        } catch (Octokit.ApiException e) {
            logger.LogError(e, "Error checking GitHub membership.");
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = e.Message
            });
        }
    }
}
