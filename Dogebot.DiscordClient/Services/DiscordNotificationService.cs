using Dogebot.DiscordClient.Adapters;
using Dogebot.DiscordClient.Contracts;
using Dogebot.DiscordClient.Models;

namespace Dogebot.DiscordClient.Services;

public class DiscordNotificationService(
    IDiscordGatewayClient gatewayClient,
    IDiscordMessageMapper mapper,
    IServerApiClient serverApiClient,
    IDiscordResponseExecutor responseExecutor,
    ILogger<DiscordNotificationService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        gatewayClient.MessageReceived += OnMessageReceivedAsync;
        logger.LogInformation("[DISCORD_NOTIFY] Notification bridge started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        gatewayClient.MessageReceived -= OnMessageReceivedAsync;
        logger.LogInformation("[DISCORD_NOTIFY] Notification bridge stopped.");
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(DiscordInboundMessage message)
    {
        try
        {
            var notification = mapper.MapToNotification(message);
            var response = await serverApiClient.NotifyAsync(notification, CancellationToken.None);
            await responseExecutor.ExecuteAsync(response, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[DISCORD_NOTIFY] Failed to process message.");
        }
    }
}


