namespace KakaoBotAT.DiscordClient.Configuration;

public class DiscordClientOptions
{
    public const string SectionName = "Discord";

    public string Token { get; set; } = string.Empty;
    public string ServerBaseUrl { get; set; } = "https://your-server-url.com/api/kakao";
    public int PollIntervalSeconds { get; set; } = 5;
    public List<string> AllowedChannelIds { get; set; } = [];
}

