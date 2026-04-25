using KakaoBotAT.Commons;

namespace KakaoBotAT.DiscordClient.Services;

public interface IServerApiClient
{
    Task<ServerResponse> NotifyAsync(ServerNotification notification, CancellationToken cancellationToken);
    Task<ServerResponse> GetPendingCommandAsync(IEnumerable<string> availableRoomIds, CancellationToken cancellationToken);
}

