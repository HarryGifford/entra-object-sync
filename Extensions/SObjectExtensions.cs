using System.Reflection;
using Havok.Attributes;
using Newtonsoft.Json;

namespace Havok.HkProjectCreate.Extensions;

public class SObjectExtensions
{
    public static IEnumerable<string> SObjectAttributes<T>() => typeof(T)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(x => x.GetCustomAttribute<GettableAttribute>()?.Gettable != false
            && x.GetCustomAttribute<JsonPropertyAttribute>() != null)
        .Select(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName!);
}
