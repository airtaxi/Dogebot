using System.Text.Json.Serialization;

namespace Dogebot.LocoClient.Models;

/// <summary>
/// WebSocket payload sent by the kakao-cli API. Type is either "ready" or "message".
/// </summary>
public class LocoRoomMessageEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("room")]
    public LocoRoom? Room { get; set; }

    [JsonPropertyName("message")]
    public LocoRoomMessage? Message { get; set; }
}
