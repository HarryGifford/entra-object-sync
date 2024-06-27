using Microsoft.Extensions.Logging;
using Octokit;

namespace Havok.Services;

public interface IGithubService
{
    /// <summary>
    /// Checks if a user is a member of a repository.
    /// </summary>
    /// <param name="owner">User or organization that owns the repo, for example "HavokPrivate".</param>
    /// <param name="repo">Name of the repository, for example "UnrealEngine".</param>
    /// <param name="username">GitHub username to check.</param>
    /// <returns>Whether the user is a member/collaborator or not.</returns>
    bool IsMemberOfRepo(string owner, string repo, string username);
}

public class GithubServiceException(string message) : Exception(message)
{
}

/// <summary>
/// Service for interacting with GitHub.
/// </summary>
/// <param name="logger">Debug/console logger.</param>
/// <param name="githubJwtFactory">Create custom JWT tokens for GitHub API.</param>
/// <param name="installationId">ID of the GitHub App installation (not the appId).</param>
public class GithubService(ILogger<GithubService>? logger, GitHubJwt.GitHubJwtFactory githubJwtFactory, int installationId) : IGithubService
{
    private GitHubClient GetClient()
    {
        // Pretty complex, but we have to authenticate first to get an access token
        // for the installation, then authenticate again with that token.

        var token = githubJwtFactory.CreateEncodedJwtToken();
        logger?.LogInformation($"Created JWT token: {token}.");
        var appClient = new GitHubClient(new ProductHeaderValue("HavokZendeskIntegration"))
        {
            Credentials = new Credentials(token, AuthenticationType.Bearer)
        };

        logger?.LogInformation("Created GitHub App client.");

        var accessToken = appClient.GitHubApps.CreateInstallationToken(installationId).Result;

        logger?.LogInformation("Created installation token.");

        var installationClient = new GitHubClient(new ProductHeaderValue("HavokZendeskIntegration"))
        {
            Credentials = new Credentials(accessToken.Token, AuthenticationType.Bearer)
        };
        return installationClient;
    }

    /// <inheritdoc />
    public bool IsMemberOfRepo(string owner, string repo, string username)
    {
        logger?.LogInformation($"Checking if {username} is a member of {repo}.");

        var client = GetClient();

        logger?.LogInformation("Created installation client.");
        
        var isMember = client.Repository.Collaborator.IsCollaborator(owner, repo, username).Result;
        logger?.LogInformation($"Is {username} a member of {repo}? {isMember}");

        return isMember;
    }
}
