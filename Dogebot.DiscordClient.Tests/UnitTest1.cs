using Dogebot.DiscordClient.Contracts;
using Dogebot.DiscordClient.Models;

namespace Dogebot.DiscordClient.Tests;

public class DiscordMessageMapperTests
{
    [Fact]
    public void MapToNotification_MapsCoreFields()
    {
        var mapper = new DiscordMessageMapper();
        var inboundMessage = new DiscordInboundMessage
        {
            ChannelId = "1234",
            ChannelName = "general",
            GuildName = "doge",
            AuthorId = "5678",
            AuthorName = "tester",
            Content = "!도움말",
            MessageId = "999",
            IsGroupChat = true,
            TimestampUnixMilliseconds = 1700000000000
        };

        var notification = mapper.MapToNotification(inboundMessage);

        Assert.Equal("message", notification.Event);
        Assert.Equal("doge/general", notification.Data.RoomName);
        Assert.Equal("1234", notification.Data.RoomId);
        Assert.Equal("5678", notification.Data.SenderHash);
        Assert.Equal("tester", notification.Data.SenderName);
        Assert.Equal("!도움말", notification.Data.Content);
        Assert.Equal("999", notification.Data.LogId);
    }
}

