using Dogebot.DiscordClient.Adapters;

namespace Dogebot.DiscordClient.Services;

public class DiscordGatewayHostedService(
    IDiscordGatewayClient gatewayClient,
    ILogger<DiscordGatewayHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[DISCORD_GATEWAY] Starting gateway...");
        await gatewayClient.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[DISCORD_GATEWAY] Stopping gateway...");
        await gatewayClient.StopAsync(cancellationToken);
    }
}


