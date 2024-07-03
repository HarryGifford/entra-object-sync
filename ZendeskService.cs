using Microsoft.Extensions.Logging;
using ZendeskApi_v2;
using ZendeskApi_v2.Models.Organizations;
using ZendeskApi_v2.Models.Users;

namespace Havok;

public interface IHkGroup
{
    public string? GetGroupId();
}

public interface IHkGroupMembership
{
    public string? GetGroupId();
    public string? GetUserId();
}

public class HkZendeskService(ILogger logger, IZendeskApi zendeskClient)
{
    public List<IHkUser> CreateOrUpdateUsers(string groupId, IEnumerable<IHkUser> users)
    {
        logger.LogInformation("Creating or updating users.");

        // Get all pages of existing users.
        var existingUsers = new List<User>();
        var existingUsersResponse = zendeskClient.Users.GetUsersInOrganization(long.Parse(groupId), 100);
        existingUsers.AddRange(existingUsersResponse.Users);
        while (existingUsersResponse.NextPage != null)
        {
            existingUsersResponse = zendeskClient.Users.GetByPageUrl<GroupUserResponse>(existingUsersResponse.NextPage);
            existingUsers.AddRange(existingUsersResponse.Users);
        }

        logger.LogInformation($"Found {existingUsers.Count} existing users.");

        // Create a dictionary of existing users by email.
        var existingUsersByEmail = existingUsers.ToDictionary(u => u.Email);

        var updateUsers = users.Where(u => existingUsersByEmail.ContainsKey(u.Email!)).Select(u => u.ToZendeskUser());
        var createUsers = users.Where(u => !existingUsersByEmail.ContainsKey(u.Email!)).Select(u => u.ToZendeskUser());

        List<IHkUser> createdUsers = [];

        // Update existing users.
        logger.LogInformation($"Updating {updateUsers.Count()} existing users.");
        foreach (var batch in updateUsers.Chunk(100))
        {
            var response = zendeskClient.Users.BulkCreateUpdateUsers(batch);
            response = WaitForJobCompletion(response);

            var failedUserUpdates = response.JobStatus.Results
                .Where(x => x.Status == "Failed")
                .Select(x => batch[x.Id]);
            
            foreach (var user in failedUserUpdates)
            {
                user.ZendeskId = zendeskClient.Users.SearchByEmail(user.Email)?.Users?.First()?.Id?.ToString();
                if (user.ZendeskId != null)
                {
                    createdUsers.Add(user);
                    zendeskClient.Users.UpdateUser(user);
                }
            }
        }

        // Create new users.
        logger.LogInformation($"Creating {createUsers.Count()} new users.");

        var existingUsersById = existingUsers
            .Where(u => u.Id != null)
            .ToDictionary(u => u.Id!.Value);

        // Run create jobs in batches of 100.
        foreach (var batch in createUsers.Chunk(100))
        {
            var response = zendeskClient.Users.BulkCreateUsers(batch);
            response = WaitForJobCompletion(response);

            var errorUsers = response.JobStatus.Results
                .Where(x => x.Status == "Failed")
                .Select(x => batch[x.Id]);
            
            foreach (var user in errorUsers)
            {
                user.ZendeskId = zendeskClient.Users.SearchByEmail(user.Email)?.Users?.FirstOrDefault(x => x.Email == user.Email)?.Id?.ToString();
                if (user.ZendeskId != null)
                {
                    createdUsers.Add(user);
                }
            }

            var createdUserIds = response.JobStatus.Results
                .Where(x => x.Status == "Created")
                .Select(x => x.Id);

            foreach (var userId in createdUserIds)
            {
                if (userId == 0)
                {
                    logger.LogError($"Failed to create user.");
                }
                var user = users.FirstOrDefault(u => u.Email == existingUsersById[userId].Email);
                if (user != null)
                {
                    user.ZendeskId = userId.ToString();
                    createdUsers.Add(user);
                }
            }
        }

        logger.LogInformation("Finished creating new users.");
        return createdUsers;
    }

    public void CreateOrUpdateGroupMemberships(string groupIdStr, IEnumerable<IHkUser> memberships)
    {
        logger.LogInformation($"Creating or updating group memberships for group {groupIdStr}.");

        var groupId = long.Parse(groupIdStr);

        // Get all pages of existing memberships.
        var existingMemberships = new List<User>();
        var existingMembershipsResponse = zendeskClient.Users.GetUsersInOrganization(groupId, 100);
        existingMemberships.AddRange(existingMembershipsResponse.Users);
        while (existingMembershipsResponse.NextPage != null)
        {
            existingMembershipsResponse = zendeskClient.Organizations.RunRequest<GroupUserResponse>(existingMembershipsResponse.NextPage, "GET");
            existingMemberships.AddRange(existingMembershipsResponse.Users);
        }

        logger.LogInformation($"Found {existingMemberships.Count} existing memberships.");

        // Create a dictionary of existing memberships by email.
        var existingMembershipsByUserId = existingMemberships.ToDictionary(m => m.Email);

        // Get users who are not already members.
        var newMemberships = memberships
            .Where(m => !existingMembershipsByUserId.ContainsKey(m.Email!));

        // Create new memberships.
        var organizationMemberships = newMemberships.Select(m => new OrganizationMembership
        {
            UserId = long.Parse(m.ZendeskId!),
            OrganizationId = groupId
        });

        logger.LogInformation($"Creating {organizationMemberships.Count()} new memberships.");

        // Run create jobs in batches of 100.
        foreach (var batch in organizationMemberships.Chunk(100))
        {
            var response = zendeskClient.Organizations.CreateManyOrganizationMemberships(batch);
            response = WaitForJobCompletion(response);
        }

        logger.LogInformation("Finished creating new memberships.");

        HashSet<string> membershipEmails = memberships.Select(m => m.Email!).ToHashSet();

        logger.LogInformation("Deleting memberships that are no longer in the list.");

        // Delete memberships that are no longer in the list.
        var membershipsToDelete = existingMemberships
            .Where(m => m.Email != null && !membershipEmails.Contains(m.Email))
            .Select(m => new OrganizationMembership
            {
                UserId = m.Id!.Value,
                OrganizationId = groupId
            });

        logger.LogInformation($"Deleting {membershipsToDelete.Count()} memberships.");

        // Run delete job.
        foreach (var user in membershipsToDelete)
        {
            zendeskClient.Organizations.RunRequest($"/users/{user.UserId}/organizations/{user.OrganizationId}", "DELETE");
            //zendeskClient.Organizations.DeleteOrganizationMembership(user.UserId!.Value, user.OrganizationId!.Value);
        }

        logger.LogInformation("Finished deleting memberships.");
    }

    private ZendeskApi_v2.Models.Shared.JobStatusResponse WaitForJobCompletion(ZendeskApi_v2.Models.Shared.JobStatusResponse job)
    {
        logger.LogInformation($"Waiting for job: {job.JobStatus.Url}");
        for (var i = 0; i < 100; i++)
        {
            Task.Delay(500).Wait();
            job = zendeskClient.JobStatuses.GetJobStatus(job.JobStatus.Id);

            if (job.JobStatus.Status == "completed")
            {
                // Log any errors.
                var errors = string.Join("\n", job.JobStatus.Results
                    .Where(x => x.Status == "Failed")
                    .Select(x => x.Details));
                if (!string.IsNullOrEmpty(errors))
                {
                    logger.LogError($"Job completed with errors: {errors}");
                }
                break;
            }

            if (job.JobStatus.Status == "failed")
            {
                logger.LogError($"Job failed with message: {job.JobStatus.Message}");
                break;
            }

            logger.LogInformation($"Job status: {job.JobStatus.Status}");
        }

        return job;
    }
}