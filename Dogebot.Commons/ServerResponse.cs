using System;
using System.Collections.Generic;
using System.Text;
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
}
