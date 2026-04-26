using KakaoBotAT.Commons;
using KakaoBotAT.DiscordClient.Adapters;

namespace KakaoBotAT.DiscordClient.Services;

public class DiscordResponseExecutor(
    IDiscordGatewayClient gatewayClient,
    ILogger<DiscordResponseExecutor> logger) : IDiscordResponseExecutor
{
    public async Task ExecuteAsync(ServerResponse response, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(response.Action))
            return;

        if (response.Action == "send_text")
        {
            if (string.IsNullOrWhiteSpace(response.RoomId) || string.IsNullOrWhiteSpace(response.Message))
            {
                logger.LogWarning("[DISCORD_EXECUTOR] Invalid send_text payload.");
                return;
            }

            await gatewayClient.SendMessageAsync(response.RoomId, response.Message, cancellationToken);
            return;
        }

        if (response.Action == "read")
        {
            logger.LogDebug("[DISCORD_EXECUTOR] read action received for room {RoomId}", response.RoomId);
            return;
        }

        if (response.Action == "error")
        {
            logger.LogWarning("[DISCORD_EXECUTOR] server error action: {Message}", response.Message);
            return;
        }

        logger.LogDebug("[DISCORD_EXECUTOR] unsupported action ignored: {Action}", response.Action);
    }
}

