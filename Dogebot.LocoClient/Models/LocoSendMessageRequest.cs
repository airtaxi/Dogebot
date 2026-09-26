using System.Text.Json.Serialization;

namespace Dogebot.LocoClient.Models;

/// <summary>
/// Request body of POST /api/rooms/{roomId}/messages.
/// </summary>
public class LocoSendMessageRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
