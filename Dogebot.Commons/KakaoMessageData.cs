using System.Text.Json.Serialization;

namespace Dogebot.Commons;

public class KakaoMessageData
{
    public const string KakaoSource = "kakao";
    public const string DiscordSource = "discord";

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [JsonPropertyName("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [JsonPropertyName("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [JsonPropertyName("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("logId")]
    public string LogId { get; set; } = string.Empty;

    [JsonPropertyName("isGroupChat")]
    public bool IsGroupChat { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }
}

