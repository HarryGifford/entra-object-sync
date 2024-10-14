using Havok.HkAuthEvents.ZdProxies;
using Havok.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NetCoreForce.Client;
namespace Havok.Functions;

/// <summary>
/// Sync a Zendesk organization field with a Salesforce picklist field.
/// </summary>
public partial class HkUpdateOrganizationFields(
    ILogger<HkUpdateOrganizationFields> logger,
    ForceClient sfClient,
    ZendeskService zendeskClient)
{
    [Function(nameof(HkUpdateOrganizationFields))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        logger.LogInformation($"C# HTTP trigger started for ${nameof(HkUpdateOrganizationFields)}.");
        
        var zendeskFieldKey = req.Query["zendesk_field_key"];
        logger.LogInformation(zendeskFieldKey);
        if (string.IsNullOrEmpty(zendeskFieldKey))
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Please provide a Zendesk field key."
            });
        }

        var salesforceProjectFieldKey = req.Query["salesforce_project_field_key"];
        logger.LogInformation(salesforceProjectFieldKey);

        if (string.IsNullOrEmpty(salesforceProjectFieldKey))
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Please provide a Salesforce project field key."
            });
        }

        var optionPrefix = req.Query["option_prefix"];
        logger.LogInformation(optionPrefix);

        if (string.IsNullOrEmpty(optionPrefix))
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = "Please provide an option prefix."
            });
        }

        // Retrieve the Salesforce picklist values for the specified field.
        var projectMetadata = sfClient
            .GetObjectDescribe("Project__c")
            .Result;

        var fieldMetadata = projectMetadata.Fields
            .FirstOrDefault(field => field.Name == salesforceProjectFieldKey);

        if (fieldMetadata == null)
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = $"Invalid Salesforce field key {salesforceProjectFieldKey}."
            });
        }

        var picklistValues = fieldMetadata.PicklistValues;

        if (picklistValues == null)
        {
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = $"Field {salesforceProjectFieldKey} has no picklist values."
            });
        }

        logger.LogInformation("Found {picklistValues} picklist values for field {salesforceProjectFieldKey}.", picklistValues, salesforceProjectFieldKey);

        // Update the Zendesk organization field with the Salesforce picklist values.

        var organizationField = new OrganizationField
        {
            Key = zendeskFieldKey!,
            CustomFieldOptions = picklistValues
                .Select(value => {
                    var label = value.Label;
                    // Special case for AI field.
                    if (label == "AI")
                    {
                        label = "Navigation";
                        value.Value = "navigation";
                    }
                    var optionValue = $"{optionPrefix}{value.Value}";
                    // Replace all special characters with underscores.
                    optionValue = SpecialCharacterRegex()
                        .Replace(optionValue, "_")
                        .ToLower()
                        .Trim(' ', '_');
                    optionValue = optionValue[..Math.Min(255, optionValue.Length)];
                    return new OrganizationFieldOption
                    {
                        Name = value.Label,
                        Value = optionValue
                    };
                })
                .ToArray()
        };

        try
        {
            var response = zendeskClient.UpdateOrganizationField(organizationField).Result;

            if (response == null)
            {
                return new BadRequestObjectResult(new HKErrorResult
                {
                    Message = "Error updating Zendesk organization field."
                });
            }

            logger.LogInformation($"Updated Zendesk organization field {zendeskFieldKey}.");
            logger.LogInformation($"Response: {response}");

            return new OkObjectResult(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error updating Zendesk organization field.");
            return new BadRequestObjectResult(new HKErrorResult
            {
                Message = e.Message
            });
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial System.Text.RegularExpressions.Regex SpecialCharacterRegex();
}