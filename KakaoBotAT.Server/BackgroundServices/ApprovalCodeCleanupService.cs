using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.BackgroundServices;

public class ApprovalCodeCleanupService(
    IServiceProvider serviceProvider,
    ILogger<ApprovalCodeCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[CLEANUP_SERVICE] Approval code cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = serviceProvider.CreateScope();
                var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
                var requestLimitService = scope.ServiceProvider.GetRequiredService<IRequestLimitService>();

                var deletedAdminCodes = await adminService.DeleteExpiredApprovalCodesAsync();
                var deletedLimitCodes = await requestLimitService.DeleteExpiredApprovalCodesAsync();

                if (deletedAdminCodes > 0 || deletedLimitCodes > 0)
                {
                    logger.LogInformation("[CLEANUP_SERVICE] Deleted {AdminCodes} admin codes and {LimitCodes} limit codes",
                        deletedAdminCodes, deletedLimitCodes);
                }
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[CLEANUP_SERVICE] Error during approval code cleanup");
            }
        }

        logger.LogInformation("[CLEANUP_SERVICE] Approval code cleanup service stopped");
    }
}
