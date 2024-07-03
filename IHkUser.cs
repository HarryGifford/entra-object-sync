namespace Havok;

public interface IHkUser
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? ZendeskId { get; set; }

    HkAuthEvents.ZdProxies.User ToZendeskUser();
}
