using Dogebot.Commons;

namespace Dogebot.DiscordClient.Services;

public interface IDiscordResponseExecutor
{
    Task ExecuteAsync(ServerResponse response, CancellationToken cancellationToken);
}


