using Havok.HkProjectCreate.Extensions;
using Havok.HkProjectCreate.SfProxies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ZendeskApi_v2.Models.Organizations;

namespace Havok.HkProjectCreate;

public class HkProjectCreate(
    ILogger<HkProjectCreate> logger,
    NetCoreForce.Client.ForceClient salesforceClient,
    ZendeskApi_v2.ZendeskApi zendeskClient)
{
    private List<ProjectExpanded> GetProjectsByIds(IEnumerable<string> ids)
    {
        var projectFields = SObjectExtensions.SObjectAttributes<Project>().ToList();
        var opportunityFields = SObjectExtensions.SObjectAttributes<Opportunity>().ToList();
        var accountFields = SObjectExtensions.SObjectAttributes<Account>().ToList();
        // Construct the query string from the fields and ids.
        var projectIdsInClause = string.Join(", ", ids.Select(id => $"'{id}'"));
        var projectFieldsInClause = projectFields;
        var opportunityFieldsInClause = opportunityFields.Select(field => $"PrimaryOpportunity__r.{field}");
        var publisherFieldsInClause = accountFields.Select(field => $"Publisher__r.{field}");
        var developerFieldsInClause = accountFields.Select(field => $"Developer__r.{field}");
        var fields = projectFieldsInClause.Concat(opportunityFieldsInClause).Concat(publisherFieldsInClause).Concat(developerFieldsInClause);
        var fieldsInClause = string.Join(", ", fields);

        var queryString = @$"SELECT {fieldsInClause} FROM Project__c WHERE Id IN ({projectIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var projects = salesforceClient.Query<ProjectExpanded>(queryString).Result;
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
        var contactRoleFields = SObjectExtensions.SObjectAttributes<OpportunityContactRole>().ToList();
        var opportunityIdsInClause = string.Join(", ", opportunityIds.Select(id => $"'{id}'"));

        var contactFields = SObjectExtensions.SObjectAttributes<Contact>().ToList();

        var queryFields = string.Join(", ", contactFields.Select(field => $"Contact.{field}").Concat(contactRoleFields));

        var queryString = @$"SELECT {queryFields} FROM OpportunityContactRole WHERE OpportunityId IN ({opportunityIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var contactRoles = salesforceClient.Query<OpportunityContactRoleExpanded>(queryString).Result;

        var contactRolesByOpportunities = contactRoles
            .GroupBy(contactRole => contactRole.OpportunityId!)
            .ToDictionary(group => group.Key, group => group
                .Where(x => x.Contact != null)
                .Select(x => x.Contact!).ToList());

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

    private List<string> GetProjectOpportunityIds(IEnumerable<string> projectIds)
    {
        var projectIdsInClause = string.Join(", ", projectIds.Select(id => $"'{id}'"));
        var queryString = @$"SELECT PrimaryOpportunity__c FROM Project__c WHERE Id IN ({projectIdsInClause})";

        logger.LogInformation($"Querying Salesforce with the following query: {queryString}");

        var projects = salesforceClient.Query<Project>(queryString).Result;

        var opportunityIds = projects
            .Select(project => project.PrimaryOpportunity__c!)
            .Distinct()
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        return opportunityIds;
    }

    private ZendeskApi_v2.Models.Shared.JobStatusResponse WaitForJobCompletion(ZendeskApi_v2.Models.Shared.JobStatusResponse job)
    {
        logger.LogInformation($"Waiting for job: {job.JobStatus.Url}");
        for (var i = 0; i < 30; i++)
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

    private void AssociateContactsWithZendeskOrganizations(IEnumerable<ProjectExpanded> projects, Dictionary<string, List<Contact>> contactsByOpportunityIds)
    {
        logger.LogInformation("Associating contacts with Zendesk organizations.");
        foreach (ProjectExpanded project in projects)
        {
            var developerId = project.PrimaryOpportunity__c;

            if (developerId != null && contactsByOpportunityIds.TryGetValue(developerId, out var developerContacts))
            {
                developerContacts = developerContacts.Where(contact => contact.Email != null).ToList();
                logger.LogInformation($"Found {developerContacts.Count()} contacts for project with id {project.Id}.");

                var contactsWithZendeskUserId = developerContacts.Where(contact => contact.Zendesk_user_id__c != null).ToList();

                var contactsWithoutZendeskUserId = developerContacts.Where(contact => contact.Zendesk_user_id__c == null).ToList();
                logger.LogInformation($"Creating {contactsWithoutZendeskUserId.Count()} Zendesk users for project with id {project.Id}.");

                // Create users for the contacts without a Zendesk user id.
                var users = contactsWithoutZendeskUserId.Select(contact => contact.ToZendeskUser());

                foreach (var user in users)
                {
                    logger.LogInformation($"Creating Zendesk user with email {user.Email}.");
                    var userResponse = zendeskClient.Users.CreateUser(user);
                    if (userResponse?.User?.Id == null)
                    {
                        logger.LogError($"Failed to create Zendesk user with email {user.Email}.");
                        return;
                    }
                    var id = userResponse.User.Id;
                    logger.LogInformation($"Created Zendesk user with email {user.Email} and id {id}.");

                    // Update the contact with the Zendesk user id.
                    var contact = developerContacts.First(x => x.Email == user.Email);
                    contact.Zendesk_user_id__c = id.ToString();
                    var contactUpdate = new Contact
                    {
                        Id = contact.Id,
                        Zendesk_user_id__c = id.ToString()
                    };
                    salesforceClient.UpdateRecord(Contact.SObjectTypeName, contact.Id, contactUpdate).Wait();
                    logger.LogInformation($"Updated contact with id {contact.Id} with Zendesk user id {id}.");
                }

                foreach (var contact in contactsWithZendeskUserId)
                {
                    // Update Zendesk user with the latest information from Salesforce.
                    var user = contact.ToZendeskUser();
                    var userResponse = zendeskClient.Users.UpdateUser(user);
                    if (userResponse?.User?.Id == null)
                    {
                        logger.LogError($"Failed to update Zendesk user with email {user.Email}.");
                        return;
                    }
                }

                /*var createUsersJob = zendeskClient.Users.BulkCreateUpdateUsers(users);

                // Wait until the users are created.
                createUsersJob = WaitForJobCompletion(createUsersJob);

                var zendeskUserIds = createUsersJob.JobStatus.Results.Where(x => x.Error == null).ToList();

                var contactsToUpdate = new List<SObject>(contactsWithoutZendeskUserId.Count);
                // Update the contacts with the Zendesk user ids.
                for (var idx = 0; idx < contactsWithoutZendeskUserId.Count; idx++)
                {
                    var zdUserIdPair = zendeskUserIds[idx];
                    var userIndex = (int)zdUserIdPair.Index;
                    if (userIndex < 0 || userIndex >= contactsWithoutZendeskUserId.Count)
                    {
                        logger.LogError($"User index {userIndex} is out of range.");
                        continue;
                    }
                    logger.LogInformation($"User index {userIndex} is in range.");
                    var currContact = contactsWithoutZendeskUserId[userIndex];
                    currContact.Zendesk_user_id__c = zdUserIdPair.Id.ToString();
                    var contact = new Contact
                    {
                        Id = currContact.Id,
                        Zendesk_user_id__c = zdUserIdPair.Id.ToString()
                    };

                    salesforceClient.UpdateRecord(Contact.SObjectTypeName, currContact.Id, contact).Wait();
                    logger.LogInformation($"Updated contact with id {currContact.Id} with Zendesk user id {zdUserIdPair.Id}.");
                }*/

                //var updateRecordsResult = salesforceClient.UpdateRecords(contactsToUpdate).Result;
                //logger.LogInformation($"Updated {updateRecordsResult.Count()} Salesforce contacts with Zendesk user ids.");

                /*foreach (var result in updateRecordsResult.Where(x => !x.Success))
                {
                    logger.LogError($"Failed to update contact with id {result.Id} with error: {result.Errors}");
                    return;
                }*/

                logger.LogInformation($"Added {contactsWithoutZendeskUserId.Count()} Salesforce contacts to Zendesk for project with id {project.Id}.");

                // Associate the contacts with the Zendesk organization.
                if (string.IsNullOrEmpty(project.Zendesk_organization_id__c))
                {
                    logger.LogError($"Project with id {project.Id} does not have a Zendesk organization id.");
                    return;
                }
                var organizationId = long.Parse(project.Zendesk_organization_id__c);

                // Get the current associations on the organization.
                var associations = zendeskClient.Organizations.GetOrganizationMembershipsByOrganizationId(organizationId, 100);

                // Remove associations that are not in the developer contacts.
                var associationsToRemove = associations.OrganizationMemberships
                    .Where(association => developerContacts.All(contact => contact.Zendesk_user_id__c != association.UserId.ToString()))
                    .Select(association => association.Id)
                    .Where(id => id != null)
                    .Select(id => id!.Value);

                if (associationsToRemove.Any())
                {
                    var destroyManyOrganizationMembershipsJob = zendeskClient.Organizations.DestroyManyOrganizationMemberships(associationsToRemove);
                    destroyManyOrganizationMembershipsJob = WaitForJobCompletion(destroyManyOrganizationMembershipsJob);
                    if (destroyManyOrganizationMembershipsJob.JobStatus.Status == "completed")
                    {
                        logger.LogInformation($"Removed {associationsToRemove.Count()} associations from the organization with id {organizationId}.");
                    }
                }

                // Add associations that are not in the organization.
                var associationsToAdd = developerContacts
                    .Where(contact => !associations.OrganizationMemberships.Any(association => association.UserId.ToString() == contact.Zendesk_user_id__c))
                    .Select(contact =>
                    {
                        var zdUserId = long.Parse(contact.Zendesk_user_id__c!);
                        logger.LogInformation($"Adding association for contact with id {contact.Id}, Zendesk user id {zdUserId} with organization {organizationId}.");
                        return new OrganizationMembership
                        {
                            UserId = zdUserId,
                            OrganizationId = organizationId
                        };
                    });

                if (associationsToAdd.Any())
                {
                    var createManyOrganizationMembershipsJob = zendeskClient.Organizations.CreateManyOrganizationMemberships(associationsToAdd);
                    createManyOrganizationMembershipsJob = WaitForJobCompletion(createManyOrganizationMembershipsJob);
                    if (createManyOrganizationMembershipsJob.JobStatus.Status == "completed")
                    {
                        logger.LogInformation($"Added {associationsToAdd.Count()} associations to the organization with id {organizationId}.");
                    }
                }
            }
        }
        logger.LogInformation("Finished associating contacts with Zendesk organizations.");
    }

    private void UpdateSalesforceZendeskProjectId(IEnumerable<Project> projects)
    {
        foreach (var project in projects)
        {
            ProjectUpdate updatedProject = new()
            {
                Zendesk_organization_id__c = project.Zendesk_organization_id__c
            };
            salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, updatedProject).Wait();
        }
    }

    [Function(nameof(HkProjectCreate))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        logger.LogInformation($"Starting {nameof(HkProjectCreate)} function.");

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

        logger.LogInformation(projectsJson);

        logger.LogInformation($"Finished {nameof(HkProjectCreate)} function.");

        //projects.ForEach(project =>
        foreach (var project in projects)
        {
            var organization = project.ToZendeskOrganization();

            if (organization.Id == null) {
                logger.LogInformation($"Creating organization for project with id {project.Id}.");
                var orgCreated = zendeskClient.Organizations.CreateOrganization(organization);
                project.Zendesk_organization_id__c = orgCreated.Organization.Id.ToString();

                logger.LogInformation($"Created organization with id {project.Zendesk_organization_id__c} for project with id {project.Id}.");
                salesforceClient.UpdateRecord(Project.SObjectTypeName, project.Id, new ProjectUpdate
                {
                    Zendesk_organization_id__c = project.Zendesk_organization_id__c
                }).Wait();
            } else {
                logger.LogInformation($"Updating organization with id {organization.Id} for project with id {project.Id}.");
                zendeskClient.Organizations.UpdateOrganization(organization);
            }

            /*var createdOrganization = zendeskClient.Organizations.CreateOrganization(organization);

            var zendeskOrganizationId = createdOrganization?.Organization?.Id;

            if (zendeskOrganizationId == null)
            {
                logger.LogError($"Failed to create organization for project with id {project.Id}.");
                return;
            }

            logger.LogInformation($"Created organization with id {zendeskOrganizationId} for project with id {project.Id}.");
            project.Zendesk_organization_id__c = zendeskOrganizationId.ToString();*/
        }

        //UpdateSalesforceZendeskProjectId(projects);
        //logger.LogInformation("Finished updating Salesforce with the Zendesk organization ids.");

        /*var projectAccountIds = GetProjectAccountIds(projectIds);
        logger.LogInformation($"Found {projectAccountIds.Count} account ids.");
        var contactsByAccountIds = GetContactsByAccountIds(projectAccountIds);
        logger.LogInformation($"Found {contactsByAccountIds.Count} account ids.");*/

        var projectOpportunityIds = GetProjectOpportunityIds(projectIds);
        logger.LogInformation($"Found {projectOpportunityIds.Count} opportunity ids.");
        var contactsByOpportunityIds = GetContactsByOpportunities(projectOpportunityIds);
        logger.LogInformation($"Found {contactsByOpportunityIds.Count} opportunity ids.");

        //AssociateContactsWithZendeskOrganizations(projects, contactsByAccountIds);
        AssociateContactsWithZendeskOrganizations(projects, contactsByOpportunityIds);
        logger.LogInformation("Finished associating contacts with Zendesk organizations.");

        return new OkObjectResult(projects);
    }
}
