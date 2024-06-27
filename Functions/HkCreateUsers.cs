using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using Havok.Schema;

namespace Havok.Functions;

public class HkCreateUsers(
    ILogger<HkProvisionOrganizations> logger,
    ZendeskApi_v2.ZendeskApi zendeskClient,
    GraphServiceClient graphClient)
{
    public List<ZendeskApi_v2.Models.Users.User> CreateOrUpdateUsers(ICollection<User> users)
    {
        var existingUsers
    }

    [Function(nameof(HkCreateUsers))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "provision-users")] HttpRequest req, FunctionContext context)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");

        string requestBody = new StreamReader(req.Body).ReadToEnd();
        var data = System.Text.Json.JsonSerializer.Deserialize<HkEndUser[]>(requestBody);

        if (data == null)
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Please provide a list of users."
            });
        }

        var userMails = data.Select(u => u.Email).ToList();

        UserCollectionResponse? existingUsersQueryResult = graphClient.Users.GetAsync(config => {
            config.QueryParameters.Filter = $"mail in ({string.Join(',', userMails)})";
            config.QueryParameters.Select = ["id", "mail"];
        }).Result;

        if (existingUsersQueryResult == null || existingUsersQueryResult.Value == null)
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Error querying existing users."
            });
        }
        
        Dictionary<string, User> existingUsers = existingUsersQueryResult.Value
            .Where(x => x.Mail != null)
            .ToDictionary(u => u.Mail!);

        foreach (var user in data)
        {
            var graphUser = user.ToGraphUser();
            if (!existingUsers.TryGetValue(user.Email, out User? value))
            {
                graphClient.Users.
            }
            else
            {
                graphClient.Users.Request().AddAsync(graphUser);
            }

            if (user.ZendeskId.HasValue)
            {
                zendeskClient.Users.UpdateUser(new ZendeskApi_v2.Models.Users.User
                {
                    Name = user.DisplayName,
                    Email = user.Email,
                    Role = "end-user",
                    ExternalId = user.EntraId?.ToString()
                });
            }
        }

        foreach (var user in data)
        {
            var graphUser = user.ToGraphUser();
            var createdUser = graphClient.Users.GetAsync(config => {
                config.QueryParameters.Filter = $"mail eq '{user.Email}'";
                config.QueryParameters.Select = ["id", "mail"];
            }).Result;

            if (user.ZendeskId.HasValue)
            {
                zendeskClient.Users.CreateUser(new ZendeskApi_v2.Models.User
                {
                    Name = user.DisplayName,
                    Email = user.Email,
                    Role = "end-user",
                    ExternalId = user.ZendeskId.Value.ToString()
                });
            }
        }
    }
}