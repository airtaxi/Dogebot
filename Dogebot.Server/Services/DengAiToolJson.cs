using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dogebot.Server.Services;

internal static class DengAiToolJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static BinaryData ToBinaryData(DengAiJsonSchema schema) =>
        BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(schema, SerializerOptions));

    public static string? ReadString(string arguments, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return null;

        using var document = JsonDocument.Parse(arguments);
        if (!document.RootElement.TryGetProperty(propertyName, out var property)) return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    public static int? ReadInt32(string arguments, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return null;

        using var document = JsonDocument.Parse(arguments);
        if (!document.RootElement.TryGetProperty(propertyName, out var property)) return null;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number)) return number;
        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number)) return number;
        return null;
    }

    public static long? ReadInt64(string arguments, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return null;

        using var document = JsonDocument.Parse(arguments);
        if (!document.RootElement.TryGetProperty(propertyName, out var property)) return null;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number)) return number;
        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out number)) return number;
        return null;
    }

    public static string Serialize(object value) =>
        JsonSerializer.Serialize(value, SerializerOptions);

    public static string SerializeOrMessage<T>(T? value, string message) where T : class =>
        value == null ? Serialize(new { Message = message }) : Serialize(value);
}
