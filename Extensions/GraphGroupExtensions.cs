using Microsoft.Graph.Models;

using ZdOrganization = ZendeskApi_v2.Models.Organizations.Organization;

namespace Havok.Extensions;

public static class GraphOrganizationExtensions
{
    /// <summary>
    /// Converts a Microsoft Graph User object to a Zendesk User object.
    /// </summary>
    public static ZdOrganization ToZendeskOrganization(this Group group)
    {
        var customFields = new Dictionary<string, object> {};

        if (group.Extensions?.ExtensionAttribute1 != null)
        {
            customFields.Add("github_username", group.OnPremisesExtensionAttributes.ExtensionAttribute1);
        }

        return new()
        {
            Name = group.DisplayName,
            Details = group.Description,
            ExternalId = group.Id,
            SharedComments = true,
            SharedTickets = true,
            OrganizationFields = customFields
        };
    }

}