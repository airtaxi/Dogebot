using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.BackgroundServices;

public class ScheduledMessageSessionCleanupService(
    IServiceProvider serviceProvider,
    ILogger<ScheduledMessageSessionCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[SCHEDULED_CLEANUP] Scheduled message session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = serviceProvider.CreateScope();
                var scheduledMessageService = scope.ServiceProvider.GetRequiredService<IScheduledMessageService>();

                var expiredSessions = scheduledMessageService.CleanupExpiredSessions();
                var staleSentEntries = scheduledMessageService.CleanupStaleSentTracking();

                if (expiredSessions > 0 || staleSentEntries > 0)
                {
                    logger.LogInformation("[SCHEDULED_CLEANUP] Cleaned up {Sessions} expired sessions and {SentEntries} stale sent-tracking entries",
                        expiredSessions, staleSentEntries);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SCHEDULED_CLEANUP] Error during scheduled message cleanup");
            }
        }

        logger.LogInformation("[SCHEDULED_CLEANUP] Scheduled message session cleanup service stopped");
    }
}
