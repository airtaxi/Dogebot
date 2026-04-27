using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Dogebot.Commons;

public class ServerNotification
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "message"; // Always "message"

    [JsonPropertyName("data")]
    public KakaoMessageData Data { get; set; } = new KakaoMessageData();
}
