using System.Text.Json.Serialization;

namespace Dogebot.Server.Services;

public sealed record DengAiJsonSchemaProperty
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Enum { get; init; }

    [JsonPropertyName("minimum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Minimum { get; init; }

    [JsonPropertyName("maximum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Maximum { get; init; }

    public static DengAiJsonSchemaProperty String(string? description = null, IReadOnlyList<string>? enumValues = null) => new()
    {
        Type = "string",
        Description = description,
        Enum = enumValues
    };

    public static DengAiJsonSchemaProperty Integer(string? description = null, int? minimum = null, int? maximum = null) => new()
    {
        Type = "integer",
        Description = description,
        Minimum = minimum,
        Maximum = maximum
    };

    public static DengAiJsonSchemaProperty Number(string? description = null, int? minimum = null, int? maximum = null) => new()
    {
        Type = "number",
        Description = description,
        Minimum = minimum,
        Maximum = maximum
    };

    public static DengAiJsonSchemaProperty Boolean(string? description = null) => new()
    {
        Type = "boolean",
        Description = description
    };
}
