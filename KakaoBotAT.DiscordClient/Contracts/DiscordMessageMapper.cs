using KakaoBotAT.Commons;
using KakaoBotAT.DiscordClient.Models;

namespace KakaoBotAT.DiscordClient.Contracts;

public class DiscordMessageMapper : IDiscordMessageMapper
{
    public ServerNotification MapToNotification(DiscordInboundMessage message) => new()
    {
        Event = "message",
        Data = new KakaoMessageData
        {
            RoomName = string.IsNullOrWhiteSpace(message.GuildName)
                ? message.ChannelName
                : $"{message.GuildName}/{message.ChannelName}",
            RoomId = message.ChannelId,
            SenderHash = message.AuthorId,
            SenderName = message.AuthorName,
            Content = message.Content,
            LogId = message.MessageId,
            IsGroupChat = message.IsGroupChat,
            Time = message.TimestampUnixMilliseconds
        }
    };
}

