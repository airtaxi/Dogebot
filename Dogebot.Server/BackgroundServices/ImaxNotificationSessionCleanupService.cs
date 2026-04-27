using Dogebot.Server.Services;

namespace Dogebot.Server.BackgroundServices;

public class ImaxNotificationSessionCleanupService(
    IServiceProvider serviceProvider,
    ILogger<ImaxNotificationSessionCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[IMAX_SESSION_CLEANUP] IMAX notification session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = serviceProvider.CreateScope();
                var imaxNotificationService = scope.ServiceProvider.GetRequiredService<IImaxNotificationService>();

                var expiredSessions = imaxNotificationService.CleanupExpiredSessions();

                if (expiredSessions > 0)
                {
                    logger.LogInformation("[IMAX_SESSION_CLEANUP] Cleaned up {Sessions} expired IMAX setup sessions",
                        expiredSessions);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[IMAX_SESSION_CLEANUP] Error during IMAX session cleanup");
            }
        }

        logger.LogInformation("[IMAX_SESSION_CLEANUP] IMAX notification session cleanup service stopped");
    }
}

