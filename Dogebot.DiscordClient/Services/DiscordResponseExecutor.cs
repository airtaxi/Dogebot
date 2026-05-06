using Dogebot.Commons;
using Dogebot.DiscordClient.Adapters;

namespace Dogebot.DiscordClient.Services;

public class DiscordResponseExecutor(
    IDiscordGatewayClient gatewayClient,
    ILogger<DiscordResponseExecutor> logger) : IDiscordResponseExecutor
{
    public async Task ExecuteAsync(ServerResponse response, CancellationToken cancellationToken)
    {
        if (response.Items.Count > 0)
        {
            foreach (var responseItem in response.Items) await ExecuteResponseItemAsync(responseItem, cancellationToken);
            return;
        }

        await ExecuteResponseItemAsync(new ServerResponseItem
        {
            Action = response.Action,
            RoomId = response.RoomId,
            Message = response.Message
        }, cancellationToken);
    }

    private async Task ExecuteResponseItemAsync(ServerResponseItem responseItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseItem.Action)) return;

        if (responseItem.Action == "send_text")
        {
            if (string.IsNullOrWhiteSpace(responseItem.RoomId) || string.IsNullOrWhiteSpace(responseItem.Message))
            {
                logger.LogWarning("[DISCORD_EXECUTOR] Invalid send_text payload.");
                return;
            }

            await gatewayClient.SendMessageAsync(responseItem.RoomId, responseItem.Message, cancellationToken);
            return;
        }

        if (responseItem.Action == "read")
        {
            logger.LogDebug("[DISCORD_EXECUTOR] read action received for room {RoomId}", responseItem.RoomId);
            return;
        }

        if (responseItem.Action == "error")
        {
            logger.LogWarning("[DISCORD_EXECUTOR] server error action: {Message}", responseItem.Message);
            return;
        }

        logger.LogDebug("[DISCORD_EXECUTOR] unsupported action ignored: {Action}", responseItem.Action);
    }
}


