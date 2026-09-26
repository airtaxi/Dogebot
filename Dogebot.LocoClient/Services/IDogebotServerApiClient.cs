using Dogebot.Commons;

namespace Dogebot.LocoClient.Services;

public interface IDogebotServerApiClient
{
    Task<ServerResponse> NotifyAsync(ServerNotification notification, CancellationToken cancellationToken);

    Task<ServerResponse> GetPendingCommandAsync(IReadOnlyCollection<string> availableRoomIds, CancellationToken cancellationToken);
}
