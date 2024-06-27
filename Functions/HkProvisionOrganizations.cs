using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

using ZdOrganization = ZendeskApi_v2.Models.Organizations.Organization;

namespace Havok.Functions;

public class IdLike<T>(long id) {
    public long Id => id;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(IdLike<T> left, IdLike<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(IdLike<T> left, IdLike<T> right)
    {
        return !(left == right);
    }
}

public class GroupId(long id) : IdLike<Group>(id) { }
public class UserId(long id) : IdLike<User>(id) { }
public class OrganizationId(long id) : IdLike<ZdOrganization>(id) { }
public class OrganizationMembershipId(long id) : IdLike<ZendeskApi_v2.Models.Organizations.OrganizationMembership>(id) { }

public class HkProvisionOrganizations(ILogger<HkProvisionOrganizations> logger, ZendeskApi_v2.ZendeskApi zendeskClient, GraphServiceClient graphClient)
{
    private Dictionary<string, UserId> UserExternalIdToId { get; } = [];

    /// <summary>
    /// Mapping from object ID to Zendesk organization id.
    /// </summary>
    private readonly Dictionary<string, long> _organizations = [];

    private void PopulateOrganizationIdMapping(IEnumerable<Group> groups, int chunkSize = 100)
    {
        // Collect all distinct user ids into a list.
        var groupIds = groups.Select(user => user.Id).Distinct().Chunk(chunkSize);
        
        foreach (var chunk in groupIds)
        {
            var ZdOrganizations = zendeskClient.Organizations.GetMultipleOrganizationsByExternalIds(chunk);
            foreach (var zdOrganization in ZdOrganizations.Organizations)
            {
                if (zdOrganization.Id.HasValue) {
                    _organizations[zdOrganization.ExternalId] = zdOrganization.Id.Value;
                }
            }
        }
    }

    private void CreateOrUpdateGroups(IEnumerable<Group> groups)
    {
        foreach (var group in groups)
        {
            if (group.Id is null)
            {
                logger.LogInformation("Group ID not found.");
                continue;
            }

            var isOrganizationCreatedInZendesk = _organizations.TryGetValue(group.Id, out var zdOrganizationId);
            if (isOrganizationCreatedInZendesk)
            {
                logger.LogInformation($"Organization {group.DisplayName} already exists in Zendesk.");
                continue;
            }

            var zdOrganization = new ZdOrganization
            {
                Name = group.DisplayName,
                ExternalId = group.Id
            };

            try {
                var createdOrganization = zendeskClient.Organizations.CreateOrganization(zdOrganization);
                if (createdOrganization is null)
                {
                    logger.LogInformation($"Organization {group.DisplayName} not created in Zendesk.");
                    continue;
                }

                _organizations[group.Id] = createdOrganization.Organization.Id!.Value;
                logger.LogInformation($"Organization {group.DisplayName} created in Zendesk.");
            } catch (System.Net.WebException e) {
                // If the organization already exists, update the mapping.
                if (e.Response is System.Net.HttpWebResponse response && response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    // Find the organization by name.
                    var zdOrganizations = zendeskClient.Organizations.GetOrganizationsStartingWith(group.DisplayName);
                    if (zdOrganizations is null || zdOrganizations.Count == 0)
                    {
                        logger.LogInformation($"Organization {group.DisplayName} not found in Zendesk.");
                        continue;
                    }
                    
                    // Find organization with exact name.
                    var zdMatchingOrganization = zdOrganizations.Organizations.FirstOrDefault(org => org.Name == group.DisplayName);
                    if (zdMatchingOrganization is null)
                    {
                        logger.LogInformation($"Organization {group.DisplayName} not found in Zendesk.");
                        continue;
                    }

                    _organizations[group.Id] = zdMatchingOrganization.Id!.Value;
                    logger.LogInformation($"Organization {group.DisplayName} found in Zendesk.");

                    // Update its external id.
                    zdMatchingOrganization.ExternalId = group.Id;
                    var updatedOrganization = zendeskClient.Organizations.UpdateOrganization(zdMatchingOrganization);
                }
            }
        }
    }

    private void CreateUsers(IEnumerable<User> users)
    {
        foreach (var user in users)
        {
            if (user.Id is null)
            {
                logger.LogError("User ID should not be null.");
                continue;
            }

            var isUserCreatedInZendesk = UserExternalIdToId.TryGetValue(user.Id, out var zdUserId);
            if (isUserCreatedInZendesk)
            {
                // logger.LogInformation($"User {user.DisplayName} already exists in Zendesk.");
                continue;
            }

            var zdUser = new ZendeskApi_v2.Models.Users.User
            {
                Name = user.DisplayName,
                Email = user.Mail,
                ExternalId = user.Id
            };

            try {
                var createdUser = zendeskClient.Users.CreateUser(zdUser);
                var zdUserIdOption = createdUser?.User?.Id;
                if (zdUserIdOption is null)
                {
                    logger.LogWarning($"User {user.DisplayName} not created in Zendesk.");
                    continue;
                }

                UserExternalIdToId[user.Id] = new UserId(zdUserIdOption.Value);
                logger.LogInformation($"User {user.DisplayName} created in Zendesk.");
            } catch (System.Net.WebException e) {
                // If the user already exists, update the mapping.
                if (e.Response is System.Net.HttpWebResponse response && response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
                {
                    // Find the user by email.
                    var zdUsers = zendeskClient.Users.SearchByEmail(user.Mail);
                    if (zdUsers is null || zdUsers.Count == 0)
                    {
                        logger.LogInformation($"User {user.DisplayName} not found in Zendesk.");
                        continue;
                    }
                    
                    // Find user with exact email.
                    var zdMatchingUser = zdUsers.Users.FirstOrDefault(zdUser => zdUser.Email == user.Mail);
                    if (zdMatchingUser is null)
                    {
                        logger.LogInformation($"User {user.DisplayName} not found in Zendesk.");
                        continue;
                    }

                    UserExternalIdToId[user.Id] = new UserId(zdMatchingUser.Id!.Value);
                    logger.LogInformation($"User {user.DisplayName} found in Zendesk.");
                }
            }
        }
    }

    private void UpdateOrganizationMemberships(IEnumerable<Group> groups)
    {
        foreach (var group in groups)
        {
            bool modified = false;
            if (group.Id is null)
            {
                logger.LogError("Group ID should not be null.");
                continue;
            }

            var isOrganizationCreatedInZendesk = _organizations.TryGetValue(group.Id, out var zdOrganizationId);
            if (!isOrganizationCreatedInZendesk)
            {
                logger.LogInformation($"Organization {group.DisplayName} not found in Zendesk.");
                continue;
            }

            var zdOrganizationMembers = zendeskClient.Organizations.GetOrganizationMembershipsByOrganizationId(zdOrganizationId);

            // Create map of existing members from the user id to the membership id.
            var existingMembers = zdOrganizationMembers.OrganizationMemberships
                .ToDictionary(
                    membership => new UserId(membership.UserId!.Value),
                    membership => new OrganizationMembershipId(membership.Id!.Value));

            if (group.Members is null)
            {
                logger.LogError($"Members should not be null. Group: \"{group.DisplayName}\".");
                continue;
            }

            // Get the new members.
            var newMembers = group.Members.Where(member => {
                if (member.Id is null)
                {
                    logger.LogError("Member ID should not be null.");
                    return false;
                }

                var hasZdUser = UserExternalIdToId.TryGetValue(member.Id, out var zdUserId);
                if (!hasZdUser)
                {
                    logger.LogInformation($"User {member.Id} not found in Zendesk.");
                    return false;
                }

                return !existingMembers.ContainsKey(zdUserId!);
            }).Select(member => new ZendeskApi_v2.Models.Organizations.OrganizationMembership
            {
                UserId = UserExternalIdToId[member.Id!].Id,
                OrganizationId = zdOrganizationId
            });

            if (newMembers.Any()) {
                var jobStatus = zendeskClient.Organizations.CreateManyOrganizationMemberships(newMembers);
                while (jobStatus.JobStatus.Status != "completed")
                {
                    Thread.Sleep(200);
                    jobStatus = zendeskClient.JobStatuses.GetJobStatus(jobStatus.JobStatus.Id);
                }
                modified = true;
            }

            // Remove members that are no longer in the group.
            var removedMembers = existingMembers.Keys.Except(group.Members
                .Where(member => member.Id is not null && UserExternalIdToId.ContainsKey(member.Id))
                .Select(member => UserExternalIdToId[member.Id!]));
            foreach (var removedMember in removedMembers)
            {
                var orgMembershipId = existingMembers[removedMember];
                zendeskClient.Organizations.DeleteOrganizationMembership(orgMembershipId.Id);
                logger.LogInformation($"User {removedMember} removed from group {group.DisplayName} in Zendesk.");
                modified = true;
            }
            if (modified) {
                logger.LogInformation($"Group {group.DisplayName} updated in Zendesk.");
            }
        }
    }

    [Function(nameof(HkProvisionOrganizations))]
    public void Run([TimerTrigger("* */20 * * * *"
    #if DEBUG
    , RunOnStartup = true
    #endif
    )] TimerInfo myTimer)
    {
        logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        Console.WriteLine($"C# Timer trigger function executed at: {DateTime.Now}");

        if (myTimer.ScheduleStatus is not null)
        {
            logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }

        logger.LogInformation("Getting groups from Microsoft Graph.");

        GroupCollectionResponse groupsResponse = graphClient.Groups.GetAsync(config => {
            config.QueryParameters.Select = ["displayName,id,description"];
            config.QueryParameters.Expand = ["members($select=id)"];
        }).Result!;

        List<Group> groups = groupsResponse.Value!;

        // Get all pages of groups.
        var pageIterator = PageIterator<Group, GroupCollectionResponse>.CreatePageIterator(
            graphClient,
            groupsResponse,
            (graphGroup) =>
            {
                groups.Add(graphGroup);
                return true;
            }
        );

        pageIterator.IterateAsync().Wait();

        logger.LogInformation($"Found {groups.Count} groups. Updating...");
        PopulateOrganizationIdMapping(groups);

        CreateOrUpdateGroups(groups);
        logger.LogInformation("Groups updated.");
        logger.LogInformation("Getting users from Microsoft Graph.");
        UserCollectionResponse usersResponse = graphClient.Users.GetAsync(config => {
            config.QueryParameters.Select = ["displayName,id,mail"];
        }).Result!;

        if (usersResponse is null)
        {
            logger.LogError("Users response is null.");
            return;
        }

        var users = usersResponse.Value!;

        // Get all pages of users.
        var userPageIterator = PageIterator<User, UserCollectionResponse>.CreatePageIterator(
            graphClient,
            usersResponse,
            (graphUser) =>
            {
                users.Add(graphUser);
                return true;
            }
        );

        userPageIterator.IterateAsync().Wait();

        logger.LogInformation($"Found {users.Count} users. Updating...");

        CreateUsers(users);

        UpdateOrganizationMemberships(groups);

        logger.LogInformation("Users updated.");

        logger.LogInformation("Provisioning complete.");
    }
}
