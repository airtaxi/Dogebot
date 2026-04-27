namespace Dogebot.DiscordClient.Models;

public class DiscordInboundMessage
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string GuildName { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public bool IsGroupChat { get; set; } = true;
    public long TimestampUnixMilliseconds { get; set; }
}


