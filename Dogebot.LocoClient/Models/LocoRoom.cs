using System.Text.Json.Serialization;

namespace Dogebot.LocoClient.Models;

/// <summary>
/// Chat room entry returned by the kakao-cli API (GET /api/rooms).
/// </summary>
public class LocoRoom
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("typeLabel")]
    public string TypeLabel { get; set; } = string.Empty;

    [JsonPropertyName("unreadCount")]
    public int UnreadCount { get; set; }

    [JsonPropertyName("lastMessage")]
    public string? LastMessage { get; set; }

    [JsonPropertyName("lastAt")]
    public long? LastAt { get; set; }
}
