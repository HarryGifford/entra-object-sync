using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.WebJobs.Extensions.AuthenticationEvents.TokenIssuanceStart;
using Microsoft.Azure.WebJobs.Extensions.AuthenticationEvents;

namespace Havok.HkAuthEvents;

public class HkAuthEventsTrigger(ILogger<HkAuthEventsTrigger> logger)
{
    [Function(nameof(HkAuthEventsTrigger))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkAuthEventsTrigger)}.");
        string requestBody = new StreamReader(req.Body).ReadToEndAsync().Result;

        if (requestBody == null)
        {
            return new BadRequestObjectResult("Please pass a request body");
        }

        logger.LogInformation(requestBody);

        //Microsoft.Azure.WebJobs.Extensions.AuthenticationEvents.TokenIssuanceStart.WebJobsTokenIssuanceStartRequest

        //object? objectData = JsonConvert.DeserializeObject(requestBody);

        HkAuthEventsTriggerRequest? eventData = JsonSerializer.Deserialize<HkAuthEventsTriggerRequest>(requestBody);

        logger.LogInformation("Successfully deserialized the request body.");

        if (eventData == null)
        {
            return new BadRequestObjectResult("Please pass a valid request body");
        }

        // Read the correlation ID from the Microsoft Entra request    
        Guid? correlationId = eventData.Data?.AuthenticationContext?.CorrelationId;

        logger.LogInformation($"Correlation ID: {correlationId}");

        // Claims to return to Microsoft Entra
        ResponseContent r = new()
        {
            Data = new()
            {
                Actions =
                [
                    new()
                    {
                        Claims = new() {
                            ApiVersion = "1.0.0",
                            CorrelationId = correlationId.ToString(),
                            Organizations = "Havok"
                        }
                    }
                ]
            }
        };

        logger.LogInformation(JsonSerializer.Serialize(r));
        logger.LogInformation("C# HTTP trigger function processed a request.");

        return new OkObjectResult(r);
    }
}

public class HkAuthEventsTriggerRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "microsoft.graph.authenticationEvent.tokenIssuanceStart";

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public WebJobsTokenIssuanceStartData Data { get; set; } = new();
}

public class WebJobsTokenIssuanceStartData
{
    [JsonPropertyName("authenticationContext")]
    public WebJobsAuthenticationEventsContext AuthenticationContext { get; set; } = new();
}

public class ResponseContent
{
    [JsonPropertyName("data")]
    public Data Data { get; set; } = new();
}

public class Data
{
    [JsonPropertyName("@odata.type")]
    public string Odatatype { get; set; } = "microsoft.graph.onTokenIssuanceStartResponseData";
    [JsonPropertyName("actions")]
    public List<Action> Actions { get; set; } = [new Action()];
}

public class Action
{
    [JsonPropertyName("@odata.type")]
    public string Odatatype { get; set; } = "microsoft.graph.tokenIssuanceStart.provideClaimsForToken";
    [JsonPropertyName("claims")]
    public Claims Claims { get; set; } = new Claims();
}

public class Claims
{
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "1.0.0";
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
    [JsonPropertyName("organizations")]
    public string? Organizations { get; set; }
}
