using KakaoBotAT.Commons;
using KakaoBotAT.DiscordClient.Models;

namespace KakaoBotAT.DiscordClient.Contracts;

public interface IDiscordMessageMapper
{
    ServerNotification MapToNotification(DiscordInboundMessage message);
}

