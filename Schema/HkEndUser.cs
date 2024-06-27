using Microsoft.Graph.Models;

namespace Havok.Schema;

[Serializable]
public class HkEndUser
{
    public required string Email { get; set; }

    public string? DisplayName { get; set; }

    public string? GivenName { get; set; }

    public string? Surname { get; set; }

    public string? JobTitle { get; set; }

    public string? Department { get; set; }

    public string? OfficeLocation { get; set; }

    public string? MobilePhone { get; set; }

    public string? BusinessPhone { get; set; }

    public string? Locale { get; set; }

    public string? IanaTimeZone { get; set; }

    public string? GitHubUsername { get; set; }

    public string? SalesforceId { get; set; }

    public long? ZendeskId { get; set; }

    public string? UsageLocation { get; set; }

    public Guid? EntraId { get; set; }

    public bool? Enabled { get; set; }

    public User ToGraphUser()
    {
        var userLocale = Locale != null ? string.Join('-', Locale.Split('_')) : null;
        return new User
        {
            AccountEnabled = true,
            DisplayName = DisplayName,
            GivenName = GivenName,
            Surname = Surname,
            JobTitle = JobTitle,
            Department = Department,
            OfficeLocation = OfficeLocation,
            MobilePhone = MobilePhone,
            BusinessPhones = [BusinessPhone],
            PreferredLanguage = userLocale,
            UsageLocation = UsageLocation,
            Mail = Email,
            OnPremisesExtensionAttributes = new OnPremisesExtensionAttributes
            {
                ExtensionAttribute1 = GitHubUsername,
                ExtensionAttribute2 = SalesforceId
            }
        };
    }
}

