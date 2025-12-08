using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.BackgroundServices;

public class ApprovalCodeCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApprovalCodeCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);

    public ApprovalCodeCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ApprovalCodeCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CLEANUP_SERVICE] Approval code cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();
                var requestLimitService = scope.ServiceProvider.GetRequiredService<IRequestLimitService>();

                var deletedAdminCodes = await adminService.DeleteExpiredApprovalCodesAsync();
                var deletedLimitCodes = await requestLimitService.DeleteExpiredApprovalCodesAsync();

                if (deletedAdminCodes > 0 || deletedLimitCodes > 0)
                {
                    _logger.LogInformation("[CLEANUP_SERVICE] Deleted {AdminCodes} admin codes and {LimitCodes} limit codes",
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
                _logger.LogError(ex, "[CLEANUP_SERVICE] Error during approval code cleanup");
            }
        }

        _logger.LogInformation("[CLEANUP_SERVICE] Approval code cleanup service stopped");
    }
}
