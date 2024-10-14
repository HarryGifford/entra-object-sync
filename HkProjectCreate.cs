using System.Net;
using Havok.HkProjectCreate.Extensions;
using Havok.HkProjectCreate.SfProxies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Havok.HkProjectCreate;

public class HkProjectCreate(
    ILogger<HkProjectCreate> logger,
    NetCoreForce.Client.ForceClient salesforceClient,
    ZendeskApi_v2.ZendeskApi zendeskClient)
{
    private readonly HkZendeskService zendeskService = new(logger, zendeskClient);

    private List<Project> GetProjectsByIds(IEnumerable<string> ids)
    {
        var projectFields = SObjectExtensions.SObjectAttributes<Project>().ToList();
        var opportunityFields = SObjectExtensions.SObjectAttributes<Opportunity>().ToList();
        var accountFields = SObjectExtensions.SObjectAttributes<Account>().ToList();
        var userFields = new List<string> {"FederationIdentifier", "Email"};
        // Construct the query string from the fields and ids.
        var projectIdsInClause = string.Join(", ", ids.Select(id => $"'{id}'"));
        var projectFieldsInClause = projectFields;
        var opportunityFieldsInClause = opportunityFields.Select(field => $"PrimaryOpportunity__r.{field}");
        var accountManagerPrimaryFieldsInClause = userFields.Select(field => $"Account_Manager_Primary__r.{field}");
        var accountManagerSecondaryFieldsInClause = userFields.Select(field => $"Account_Manager_Secondary__r.{field}");
        var accountManagerTertiaryFieldsInClause = userFields.Select(field => $"Account_Manager_Tertiary__r.{field}");

        var publisherFieldsInClause = accountFields.Select(field => $"Publisher__r.{field}");
        var developerFieldsInClause = accountFields.Select(field => $"Developer__r.{field}");
        var fields = projectFieldsInClause
            .Concat(opportunityFieldsInClause)
            .Concat(publisherFieldsInClause)
            .Concat(developerFieldsInClause)
            .Concat(accountManagerPrimaryFieldsInClause)
            .Concat(accountManagerSecondaryFieldsInClause)
            .Concat(accountManagerTertiaryFieldsInClause);
        var fieldsInClause = string.Join(", ", fields);
        var queryString = projectIdsInClause == "'ALL'"
            ? @$"SELECT {fieldsInClause} FROM Project__c WHERE Support_Management_Status__c NOT IN ('Cancelled', 'With Sales - Qualification') AND Project_Status__c NOT IN ('Cancelled', 'Lost')"
            //? @$"SELECT {fieldsInClause} FROM Project__c WHERE Support_Management_Status__c NOT IN ('With Sales - Qualification')"
            
            : @$"SELECT {fieldsInClause} FROM Project__c WHERE Id IN ({projectIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var projects = salesforceClient.Query<Project>(queryString).Result;
        return projects;
    }

    private Dictionary<string, List<Contact>> GetContactsByAccountIds(IEnumerable<string> accountIds)
    {
        var contactFields = SObjectExtensions.SObjectAttributes<Contact>().ToList();
        var contactFieldsInClause = string.Join(", ", contactFields);
        var accountIdsInClause = string.Join(", ", accountIds.Select(id => $"'{id}'"));

        var queryString = @$"SELECT {contactFieldsInClause} FROM Contact WHERE AccountId IN ({accountIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var contacts = salesforceClient.Query<Contact>(queryString).Result;

        var contactsByAccountIds = contacts
            .GroupBy(contact => contact.AccountId!)
            .ToDictionary(group => group.Key, group => group.DistinctBy(x => x.Email).ToList());

        return contactsByAccountIds;
    }

    private Dictionary<string, List<Contact>> GetContactsByOpportunities(IEnumerable<string> opportunityIds)
    {
        if (!opportunityIds.Any())
        {
            return [];
        }

        var contactRoleFields = SObjectExtensions.SObjectAttributes<OpportunityContactRole>().ToList();

        Dictionary<string, List<Contact>> contactRolesByOpportunities = [];

        var contactFields = SObjectExtensions.SObjectAttributes<Contact>().ToList();

        foreach (var opportunityId in opportunityIds)
        {
            var queryFields = string.Join(", ", contactFields.Select(field => $"Contact.{field}").Concat(contactRoleFields));

            var queryString = @$"SELECT {queryFields} FROM OpportunityContactRole WHERE OpportunityId = '{opportunityId}' LIMIT 1000";

            logger.LogInformation($"Querying Salesforce with the following query: {queryString}");
            
            var contactRoles = salesforceClient.Query<OpportunityContactRole>(queryString).Result;
            contactRolesByOpportunities[opportunityId] = contactRoles.Select(x => x.Contact!).ToList();
        }
/*
        var opportunityIdsInClause = string.Join(", ", opportunityIds.Select(id => $"'{id}'"));

        var contactFields = SObjectExtensions.SObjectAttributes<Contact>().ToList();

        var queryFields = string.Join(", ", contactFields.Select(field => $"Contact.{field}").Concat(contactRoleFields));

        var queryString = @$"SELECT {queryFields} FROM OpportunityContactRole WHERE OpportunityId IN ({opportunityIdsInClause}) LIMIT 1000";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var contactRoles = salesforceClient.Query<OpportunityContactRole>(queryString).Result;

        var contactRolesByOpportunities = contactRoles
            .GroupBy(contactRole => contactRole.OpportunityId!)
            .ToDictionary(group => group.Key, group => group
                .Where(x => x.Contact != null)
                .Select(x => x.Contact!).ToList());
*/
        return contactRolesByOpportunities;
    }

    private List<string> GetProjectAccountIds(IEnumerable<string> projectIds)
    {
        var projectIdsInClause = string.Join(", ", projectIds.Select(id => $"'{id}'"));
        var queryString = @$"SELECT Publisher__c, Developer__c FROM Project__c WHERE Id IN ({projectIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var projects = salesforceClient.Query<Project>(queryString).Result;

        var accountIds = projects
            .SelectMany(project => new[] { project.Publisher__c!, project.Developer__c! })
            .Distinct()
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        return accountIds;
    }

    private List<string> GetProjectOpportunityIds(IEnumerable<Project> projects)
    {
        /*var projectIdsInClause = string.Join(", ", projectIds.Select(id => $"'{id}'"));
        var queryString = @$"SELECT PrimaryOpportunity__c FROM Project__c WHERE Id IN ({projectIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var projects = salesforceClient.Query<Project>(queryString).Result;*/

        var opportunityIds = projects
            .Select(project => project.PrimaryOpportunity__c!)
            .Distinct()
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        return opportunityIds;
    }
    private ObjectResult? AssociateContactsWithZendeskOrganizations(IEnumerable<Project> projects, Dictionary<string, List<Contact>> contactsByOpportunityIds)
    {
        logger.LogInformation("Associating contacts with Zendesk organizations.");
        foreach (Project project in projects)
        {
            var developerId = project.PrimaryOpportunity__c;

            if (developerId == null || !contactsByOpportunityIds.TryGetValue(developerId, out List<Contact>? developerContacts))
            {
                developerContacts = [];
            }
            developerContacts = developerContacts.Where(contact => contact.Email != null).ToList();
            logger.LogInformation($"Found {developerContacts.Count()} contacts for project with id {project.Id}.");

            var contactsWithZendeskUserId = developerContacts.Where(contact => contact.ZendeskId != null).ToList();

            var contactsWithoutZendeskUserId = developerContacts.Where(contact => contact.ZendeskId == null).ToList();
            logger.LogInformation($"Creating {contactsWithoutZendeskUserId.Count()} Zendesk users for project with id {project.Id}.");

            if (project.PrimaryOpportunity__r != null)
            {
                project.PrimaryOpportunity__r.OpportunityContactRoles = developerContacts
                    .Select(contact => new OpportunityContactRole
                    {
                        Id = null!,
                        OpportunityId = project.PrimaryOpportunity__c,
                        Contact = contact,
                        ContactId = contact.Id
                    }).ToList();
            }

            // Create users for the contacts without a Zendesk user id.
            var users = contactsWithoutZendeskUserId.Select(contact => contact.ToZendeskUser());

            var createdUsers = zendeskService.CreateOrUpdateUsers(project.Zendesk_organization_id__c!, users);

            var developerContactsByEmail = developerContacts.ToDictionary(x => x.Email!);

            // Update Salesforce contacts with the Zendesk user id.

            logger.LogInformation($"Updating {createdUsers.Count()} Salesforce contacts with Zendesk user ids.");
            foreach (var createdUser in createdUsers)
            {
                var contact = developerContactsByEmail.GetValueOrDefault(createdUser.Email!);
                if (contact == null)
                {
                    logger.LogError($"Failed to find contact with email {createdUser.Email}.");
                    return null;
                }
                contact.ZendeskId = createdUser.ZendeskId;
                var contactUpdate = new Contact
                {
                    Id = contact.Id,
                    ZendeskId = createdUser.ZendeskId
                };
                try {
                    salesforceClient.UpdateRecord(Contact.SObjectTypeName, contact.Id, contactUpdate).Wait();
                } catch (Exception e) {
                    logger.LogError($"Failed to update contact with id {contact.Id}.");
                    return new BadRequestObjectResult($"Failed to update contact with id {contact.Id}: {e.Message}");
                }
                logger.LogInformation($"Updated contact with id {contact.Id} with Zendesk user id {createdUser.ZendeskId}.");
            }

            logger.LogInformation($"Added {contactsWithoutZendeskUserId.Count()} Salesforce contacts to Zendesk for project with id {project.Id}.");

            zendeskService.CreateOrUpdateGroupMemberships(
                project.Zendesk_organization_id__c!,
                developerContacts.Where(x => x.Id != null && x.Email != null)
            );
        }
        logger.LogInformation("Finished associating contacts with Zendesk organizations.");
        return null;
    }

    private void UpdateSalesforceZendeskProjectId(IEnumerable<Project> projects)
    {
        foreach (var project in projects)
        {
            Project updatedProject = new()
            {
                Id = null!,
                Zendesk_organization_id__c = project.Zendesk_organization_id__c
            };
            salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, updatedProject).Wait();
        }
    }

    public void CreateSalesforceUsersFromCommunityNames(IEnumerable<string> communityNames)
    {
        List<string> ids = [];
        foreach (var communityName in communityNames)
        {
            if (communityName == null)
            {
                continue;
            }
            var queryString = @$"SELECT Id, Name, Email, FederationIdentifier FROM User WHERE CommunityNickname = '{communityName}'";
            var users = salesforceClient.Query<User>(queryString).Result;
            User user;
            if (users.Count != 0)
            {
                user = new User
                {
                    Id = users[0].Id,
                    ProfileId = "00e20000000gxGb"
                };
                salesforceClient.UpdateRecord(User.SObjectTypeName, user.Id, user).Wait();
                continue;
            }
            var firstName = communityName.Split('.')[0];
            var lastNames = string.Join(' ', communityName.Split('.').Skip(1));
            user = new User
            {
                Id = null!,
                FirstName = firstName,
                LastName = lastNames,
                CommunityNickname = communityName,
                Username = communityName + "@havok.com",
                Alias = communityName[..8],
                Email = communityName + "@havok.com",
                IsActive = false,
                TimeZoneSidKey = "America/Los_Angeles",
                LocaleSidKey = "en_GB",
                LanguageLocaleKey = "en_US",
                EmailEncodingKey = "UTF-8",
                ProfileId = "00e20000000gxGb",
            };
            var response = salesforceClient.CreateRecord(User.SObjectTypeName, user).Result;
            logger.LogInformation($"Created user with id {response.Id}.");
            ids.Add(response.Id);
        }
        logger.LogInformation($"Created {ids.Count} users.");
    }

    private static bool IsProjectPrimarySecondaryInconsistent(Project project, Dictionary<string, User> usersByCommunityNickname)
    {
        if (project.Account_Manager_Primary__c is null && project.Primary_Account_Manager__c is not null and not "TBD")
        {
            return true;
        }

        if (project.Account_Manager_Secondary__c is null && project.Secondary_Account_Manager__c is not null and not "TBD")
        {
            return true;
        }

        if (project.Account_Manager_Primary__c is not null && project.Primary_Account_Manager__c is not null && project.Primary_Account_Manager__c != "TBD" && usersByCommunityNickname[project.Primary_Account_Manager__c].Id != project.Account_Manager_Primary__c)
        {
            return true;
        }

        if (project.Account_Manager_Secondary__c is not null && project.Secondary_Account_Manager__c is not null && project.Secondary_Account_Manager__c != "TBD" && usersByCommunityNickname[project.Secondary_Account_Manager__c].Id != project.Account_Manager_Secondary__c)
        {
            return true;
        }

        return false;
    }

    public void UpdatePrimarySecondaryAccountManagerFields()
    {
        // Iterate through all projects and update the Primary Account Manager and Secondary Account Manager fields.
        var queryString = "SELECT Id, Primary_Account_Manager__c, Secondary_Account_Manager__c, Account_Manager_Primary__c, Account_Manager_Secondary__c FROM Project__c";
        var projects = salesforceClient.Query<Project>(queryString).Result;
        var users = salesforceClient.Query<User>("SELECT Id, CommunityNickname FROM User").Result;
        var usersByCommunityNickname = users.Where(x => !string.IsNullOrWhiteSpace(x.CommunityNickname)).ToDictionary(user => user.CommunityNickname!);
        
        var inconsistentProjects = projects
            .Where(x => IsProjectPrimarySecondaryInconsistent(x, usersByCommunityNickname))
            .ToList();

        logger.LogInformation($"Found {inconsistentProjects.Count} inconsistent projects.");

        foreach (var project in projects)
        {
            var oldPrimaryAccountManager = project.Primary_Account_Manager__c;
            var oldSecondaryAccountManager = project.Secondary_Account_Manager__c;
            if (oldPrimaryAccountManager == "TBD")
            {
                oldPrimaryAccountManager = null;
            }
            if (oldSecondaryAccountManager == "TBD")
            {
                oldSecondaryAccountManager = null;
            }
            var primaryAccountManager = oldPrimaryAccountManager != null ? usersByCommunityNickname[oldPrimaryAccountManager] : null;
            var secondaryAccountManager = oldSecondaryAccountManager != null ? usersByCommunityNickname[oldSecondaryAccountManager] : null;
            var isUpdated = false;
            if (primaryAccountManager != null && project.Account_Manager_Primary__c != primaryAccountManager.Id)
            {
                project.Account_Manager_Primary__c = primaryAccountManager.Id;
                isUpdated = true;
            }
            if (secondaryAccountManager != null && project.Account_Manager_Secondary__c != secondaryAccountManager.Id)
            {
                project.Account_Manager_Secondary__c = secondaryAccountManager.Id;
                isUpdated = true;
            }
            if (isUpdated)
            {
                logger.LogInformation($"Updating project with id {project.Id}.");
                logger.LogInformation($"Primary Account Manager: {oldPrimaryAccountManager} -> {project.Account_Manager_Primary__c}");
                logger.LogInformation($"Secondary Account Manager: {oldSecondaryAccountManager} -> {project.Account_Manager_Secondary__c}");

                string projectId = project.Id;
                var updatedProject = new Project
                {
                    Id = null!,
                    Account_Manager_Primary__c = project.Account_Manager_Primary__c,
                    Account_Manager_Secondary__c = project.Account_Manager_Secondary__c
                };
                try {
                    salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, updatedProject).Wait();
                }
                catch (Exception e)
                {
                    if (e.Message.Contains("Make sure the Physics Engine field is either Physics (hknp), Physics2012, or Other."))
                    {
                        updatedProject.Physics_Engine__c = "Physics2012";
                        salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, updatedProject).Wait();
                    }
                    else
                    {
                        logger.LogError($"Failed to update project with id {projectId}.");
                        throw;
                    }
                }
                logger.LogInformation($"Updated project with id {projectId}.");
            }
        }
        
    }

    [Function(nameof(HkProjectCreate))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        logger.LogInformation($"Starting {nameof(HkProjectCreate)} function.");

        // UpdatePrimarySecondaryAccountManagerFields();

        if (!req.Query.TryGetValue("ids", out var projectIdsValues))
        {
            return new BadRequestObjectResult("Please set the 'ids' query parameter to the Salesforce project ids.");
        }

        var projectIdsStr = projectIdsValues.ToString();

        if (string.IsNullOrWhiteSpace(projectIdsStr))
        {
            return new BadRequestObjectResult("The 'ids' query parameter must not be empty.");
        }

        var projectIds = projectIdsStr.Split(',');

        var projects = GetProjectsByIds(projectIds);
        projectIds = projects.Select(x => x.Id).ToArray();

        if (projects == null || projects.Count == 0)
        {
            return new NotFoundObjectResult("No projects found with the specified ids.");
        }

        // Ensure there are the same number of projects as the number of ids.
        if (projects.Count != projectIds.Length)
        {
            return new NotFoundObjectResult("Some projects were not found with the specified ids.");
        }

        logger.LogInformation($"Found {projects.Count} projects.");

        // Serialize the projects to JSON.
        var projectsJson = JsonConvert.SerializeObject(projects);

        //logger.LogInformation(projectsJson);

        logger.LogInformation($"Finished {nameof(HkProjectCreate)} function.");

        //projects.ForEach(project =>
        foreach (var project in projects)
        {
            string? oldZendesk_organization_id__c = project.Zendesk_organization_id__c;
            var organization = project.ToZendeskOrganization();

            if (organization.Id == null)
            {
                logger.LogInformation($"Creating organization for project with id {project.Id}.");
                try
                {
                    var orgCreated = zendeskClient.Organizations.CreateOrUpdateOrganization(organization);
                    project.Zendesk_organization_id__c = orgCreated.Organization.Id.ToString();
                }
                catch (WebException e)
                {
                    logger.LogError($"Check if organization for {project.Id} already exists.");
                    var matchingOrg = zendeskClient.Organizations.GetOrganizationsStartingWith(organization.Name)?.Organizations.FirstOrDefault(x => x.Name == organization.Name);
                    if (matchingOrg != null)
                    {
                        project.Zendesk_organization_id__c = matchingOrg.Id.ToString();
                        organization.Id = matchingOrg.Id;
                        zendeskClient.Organizations.UpdateOrganization(organization);
                    }
                    else
                    {
                        // Return the error if the organization does not exist.
                        return new BadRequestObjectResult(e.Message);
                    }
                }

                logger.LogInformation($"Created organization with id {project.Zendesk_organization_id__c} for project with id {project.Id}.");

                if (project.Zendesk_organization_id__c != oldZendesk_organization_id__c) {
                    try {
                        salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, new Project
                        {
                            Id = null!,
                            Zendesk_organization_id__c = project.Zendesk_organization_id__c,
                            // Set the Physics Engine to the default value if it is null.
                            Physics_Engine__c = project.Physics_Engine__c ?? "Physics2012"
                        }).Wait();
                    } catch (Exception e) {
                        logger.LogError($"Failed to update project with id {project.Id}.");
                        return new BadRequestObjectResult($"Failed to update project with id {project.Id}: {e.Message}");
                    }
                }
            }
            else
            {
                logger.LogInformation($"Updating organization with id {organization.Id} for project with id {project.Id}.");
                zendeskClient.Organizations.UpdateOrganization(organization);
            }
        }

        var projectOpportunityIds = GetProjectOpportunityIds(projects);
        logger.LogInformation($"Found {projectOpportunityIds.Count} opportunity ids.");
        var contactsByOpportunityIds = GetContactsByOpportunities(projectOpportunityIds);
        logger.LogInformation($"Found {contactsByOpportunityIds.Count} opportunity ids.");

        //AssociateContactsWithZendeskOrganizations(projects, contactsByAccountIds);
        try {
            var errorResult = AssociateContactsWithZendeskOrganizations(projects, contactsByOpportunityIds);
            if (errorResult != null)
            {
                return errorResult;
            }
        } catch (Exception e) {
            return new BadRequestObjectResult(e.Message);
        }

        logger.LogInformation("Finished associating contacts with Zendesk organizations.");

        return new OkObjectResult(projects);
    }
}
