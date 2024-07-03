using Microsoft.Graph.Models;

using ZdUser = ZendeskApi_v2.Models.Users.User;

namespace Havok.Extensions;

public static class GraphUserExtensions
{
    /// <summary>
    /// Converts a Microsoft Graph User object to a Zendesk User object.
    /// </summary>
    public static ZdUser ToZendeskUser(this User endUser)
    {
        var customFields = new Dictionary<string, object> {};

        if (endUser.OnPremisesExtensionAttributes?.ExtensionAttribute1 != null)
        {
            customFields.Add("github_username", endUser.OnPremisesExtensionAttributes.ExtensionAttribute1);
        }

        if (endUser.OnPremisesExtensionAttributes?.ExtensionAttribute2 != null)
        {
            customFields.Add("salesforce_contact_id", endUser.OnPremisesExtensionAttributes.ExtensionAttribute2);
        }

        if (endUser.JobTitle != null)
        {
            customFields.Add("title", endUser.JobTitle);
        }

        if (endUser.Department != null)
        {
            customFields.Add("department", endUser.Department);
        }

        if (endUser.OfficeLocation != null)
        {
            customFields.Add("office_location", endUser.OfficeLocation);
        }

        if (!string.IsNullOrEmpty(endUser.UsageLocation))
        {
            customFields.Add("usage_location", $"usage_location_{endUser.UsageLocation}");
        }

        return new()
        {
            Name = endUser.DisplayName,
            Active = endUser.AccountEnabled,
            Email = endUser.Mail,
            ExternalId = endUser.Id,
            Phone = endUser.MobilePhone ?? endUser.BusinessPhones?.FirstOrDefault(),
            CustomFields = customFields
        };
    }
}
