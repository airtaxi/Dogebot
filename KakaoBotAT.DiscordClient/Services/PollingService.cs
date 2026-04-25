using KakaoBotAT.DiscordClient.Adapters;
using KakaoBotAT.DiscordClient.Configuration;
using Microsoft.Extensions.Options;

namespace KakaoBotAT.DiscordClient.Services;

public class PollingService(
    IDiscordGatewayClient gatewayClient,
    IServerApiClient serverApiClient,
    IDiscordResponseExecutor responseExecutor,
    IOptions<DiscordClientOptions> options,
    ILogger<PollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, options.Value.PollIntervalSeconds);
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        logger.LogInformation("[DISCORD_POLL] Polling started. interval={Interval}s", intervalSeconds);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var roomIds = gatewayClient.GetAvailableRoomIds();
                if (roomIds.Count == 0)
                    continue;

                var response = await serverApiClient.GetPendingCommandAsync(roomIds, stoppingToken);
                await responseExecutor.ExecuteAsync(response, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[DISCORD_POLL] Polling cancelled.");
        }
    }
}

