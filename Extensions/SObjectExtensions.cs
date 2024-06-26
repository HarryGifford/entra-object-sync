using System.Reflection;
using Newtonsoft.Json;

namespace Havok.HkProjectCreate.Extensions;

public class SObjectExtensions
{
    public static IEnumerable<string> SObjectAttributes<T>() => typeof(T)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Select(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName!)
        .Where(x => x != null);
}
