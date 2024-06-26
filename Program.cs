using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ZendeskApi_v2;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddLogging();
        // Add Salesforce service for Salesforce REST API v61.0 and authorize with OAuth2.
        services.AddSingleton(serviceProvider => {
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

        services.AddScoped<IZendeskApi, ZendeskApi>();
        ZendeskApi zendeskApi = new(
            context.Configuration["Zendesk.EndpointUri"],
            context.Configuration["Zendesk.Username"],
            context.Configuration["Zendesk.ApiToken"]
        );
        services.AddSingleton(zendeskApi);
    })
    .Build();

host.Run();
