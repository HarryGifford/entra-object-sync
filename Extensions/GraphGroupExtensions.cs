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

        List<string> tags = [];

        group.Extensions?.ForEach(extension =>
        {
            if (extension.Id == "exta4ltzss2_havokProject")
            {
                string? havokSdkVersion = extension.BackingStore.Get<string>("havokSdkVersion");
                if (havokSdkVersion != null)
                {
                    customFields.Add("havok_project", havokSdkVersion);
                }

                string? havokUeVersion = extension.BackingStore.Get<string>("havokUeVersion");
                if (havokUeVersion != null)
                {
                    customFields.Add("havok_ue_version", havokUeVersion);
                }

                bool? hasHavokNavigation = extension.BackingStore.Get<bool>("hasHavokNavigation");
                if (hasHavokNavigation != null)
                {
                    tags.Add("product_navigation");
                }

                bool? hasHavokPhysics = extension.BackingStore.Get<bool>("hasHavokPhysics");
                if (hasHavokPhysics != null)
                {
                    tags.Add("product_physics");
                }

                bool? hasHavokCloth = extension.BackingStore.Get<bool>("hasHavokCloth");
                if (hasHavokCloth != null)
                {
                    tags.Add("product_cloth");
                }
            }
        });

        return new()
        {
            Name = group.DisplayName,
            Details = group.Description,
            ExternalId = group.Id,
            SharedComments = true,
            SharedTickets = true,
            OrganizationFields = customFields,
            Tags = tags
        };
    }

}