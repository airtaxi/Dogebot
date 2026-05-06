using System.Text.Json.Serialization;

namespace Dogebot.Commons;

public class ServerResponse
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty; // "send_text", "read"

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty; // Optional

    [JsonPropertyName("items")]
    public List<ServerResponseItem> Items { get; set; } = [];
}

public class ServerResponseItem
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
