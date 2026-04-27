using System.Text.Json.Serialization;

namespace Dogebot.Server.Models;

public class FortuneItem
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

