using KakaoBotAT.Commons;

namespace KakaoBotAT.DiscordClient.Services;

public interface IDiscordResponseExecutor
{
    Task ExecuteAsync(ServerResponse response, CancellationToken cancellationToken);
}

