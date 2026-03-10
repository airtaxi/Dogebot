using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.BackgroundServices;

public class ScheduledMessageSessionCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledMessageSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public ScheduledMessageSessionCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledMessageSessionCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SCHEDULED_CLEANUP] Scheduled message session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var scheduledMessageService = scope.ServiceProvider.GetRequiredService<IScheduledMessageService>();

                var expiredSessions = scheduledMessageService.CleanupExpiredSessions();
                var staleSentEntries = scheduledMessageService.CleanupStaleSentTracking();

                if (expiredSessions > 0 || staleSentEntries > 0)
                {
                    _logger.LogInformation("[SCHEDULED_CLEANUP] Cleaned up {Sessions} expired sessions and {SentEntries} stale sent-tracking entries",
                        expiredSessions, staleSentEntries);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SCHEDULED_CLEANUP] Error during scheduled message cleanup");
            }
        }

        _logger.LogInformation("[SCHEDULED_CLEANUP] Scheduled message session cleanup service stopped");
    }
}
