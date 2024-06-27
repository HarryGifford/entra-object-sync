using System.Text.Json.Serialization;

namespace Havok.Schema;

[Serializable]
public class HKErrorResult {

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
