using KakaoBotAT.DiscordClient.Models;

namespace KakaoBotAT.DiscordClient.Adapters;

public interface IDiscordGatewayClient
{
    event Func<DiscordInboundMessage, Task>? MessageReceived;

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task SendMessageAsync(string channelId, string message, CancellationToken cancellationToken);
    IReadOnlyList<string> GetAvailableRoomIds();
}

