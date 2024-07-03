namespace Havok.Attributes;

/// <summary>
/// Can the field be queried from the Salesforce API.
/// </summary>
[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false,
    AllowMultiple = false)]
public class GettableAttribute(bool gettable) : Attribute
{
    public bool Gettable => gettable;
}
