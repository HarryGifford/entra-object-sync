using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ZendeskApi_v2;
using Havok.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddLogging();

        var loggerFactory = new LoggerFactory();
        var logger = loggerFactory.CreateLogger("Startup");

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        logger?.LogInformation("Adding Salesforce service.");
        // Add Salesforce service for Salesforce REST API v61.0 and authorize with OAuth2.
        services.AddSingleton(serviceProvider =>
        {
            var restApiVersion = context.Configuration["Salesforce.RestApiVersion"];
            var authClient = new NetCoreForce.Client.AuthenticationClient(restApiVersion);
            var consumerKey = context.Configuration["Salesforce.ConsumerKey"];
            var consumerSecret = context.Configuration["Salesforce.ConsumerSecret"];
            var tokenEndpoint = context.Configuration["Salesforce.TokenEndpoint"];
            var instanceUrl = context.Configuration["Salesforce.InstanceUrl"];
            authClient.ClientCredentialsAsync(consumerKey, consumerSecret, tokenEndpoint).Wait();
            var client = new NetCoreForce.Client.ForceClient(instanceUrl, restApiVersion, authClient.AccessInfo.AccessToken);
            return client;
        });

        logger?.LogInformation("Adding Zendesk service.");
        services.AddScoped<IZendeskApi, ZendeskApi>();
        var zendeskApi = new ZendeskApi(
            context.Configuration["Zendesk.EndpointUri"],
            context.Configuration["Zendesk.Username"],
            context.Configuration["Zendesk.ApiToken"]
        );
        services.AddSingleton(zendeskApi);

        logger?.LogInformation("Adding Github service.");

        services.AddScoped<IGithubService, GithubService>();
        services.AddSingleton(serviceProvider =>
        {
            var githubPrivateKey = context.Configuration["GitHub.AppPrivateKey"]
                ?? throw new Exception("GitHub.AppPrivateKey is null.");
            var appIdStr = context.Configuration["GitHub.AppId"]
                ?? throw new Exception("GitHub.AppId is null.");
            var appId = int.Parse(appIdStr);

            logger?.LogInformation($"GitHub app ID: {appId}");

            var installationIdStr = context.Configuration["GitHub.InstallationId"]
                ?? throw new Exception("GitHub.InstallationId is null.");
            var installationId = int.Parse(installationIdStr);

            logger?.LogInformation($"GitHub installation ID: {installationId}");

            var githubJwtFactory = new GitHubJwt.GitHubJwtFactory(
                new GitHubJwt.StringPrivateKeySource(githubPrivateKey),
                new GitHubJwt.GitHubJwtFactoryOptions
                {
                    AppIntegrationId = appId,
                    ExpirationSeconds = 500
                }
            ) ?? throw new Exception("GitHubJwtFactory is null.");
            var githubServiceLogger = loggerFactory.CreateLogger<GithubService>();
            var githubService = new GithubService(githubServiceLogger, githubJwtFactory, installationId);
            return githubService;
        });

        logger?.LogInformation("Adding Microsoft Graph service.");

        string authority = context.Configuration["AzureAd.Authority"] ?? throw new Exception("AzureAd.Authority is null.");
        string tenantId = context.Configuration["AzureAd.TenantId"] ?? throw new Exception("AzureAd.TenantId is null.");
        string clientId = context.Configuration["AzureAd.ClientId"] ?? throw new Exception("AzureAd.ClientId is null.");
        string clientSecret = context.Configuration["AzureAd.ClientSecret"] ?? throw new Exception("AzureAd.ClientSecret is null.");
        string? scopesStr = context.Configuration["AzureAd.Scopes"];

        var clientSecretCredential = new ClientSecretCredential(
            tenantId,
            clientId,
            clientSecret,
            new TokenCredentialOptions()
            {
                AuthorityHost = new Uri(authority)
            });
        string[] scopes = scopesStr?.Split(",") ?? ["https://graph.microsoft.com/.default"];
        var graphServiceClient = new GraphServiceClient(clientSecretCredential, scopes);
        services.AddSingleton(graphServiceClient);

        logger?.LogInformation("Done adding services.");
    })
    .Build();

host.Run();
