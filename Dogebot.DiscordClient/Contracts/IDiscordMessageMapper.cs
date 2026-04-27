using Dogebot.Commons;
using Dogebot.DiscordClient.Models;

namespace Dogebot.DiscordClient.Contracts;

public interface IDiscordMessageMapper
{
    ServerNotification MapToNotification(DiscordInboundMessage message);
}


