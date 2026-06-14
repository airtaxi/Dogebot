using System.Text.Json.Serialization;

namespace Dogebot.Server.Services;

public sealed record DengAiJsonSchema
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "object";

    [JsonPropertyName("properties")]
    public IReadOnlyDictionary<string, DengAiJsonSchemaProperty> Properties { get; init; } = new Dictionary<string, DengAiJsonSchemaProperty>();

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Required { get; init; }

    [JsonPropertyName("additionalProperties")]
    public bool AdditionalProperties { get; init; }

    public static DengAiJsonSchema Object(IReadOnlyDictionary<string, DengAiJsonSchemaProperty>? properties = null, IReadOnlyList<string>? required = null) => new()
    {
        Properties = properties ?? new Dictionary<string, DengAiJsonSchemaProperty>(),
        Required = required,
        AdditionalProperties = false
    };
}
