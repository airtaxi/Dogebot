using System.Text;
using System.Text.Json;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.BackgroundServices;

public class ImaxNotificationCheckService(
    IServiceProvider serviceProvider,
    IHttpClientFactory httpClientFactory,
    ILogger<ImaxNotificationCheckService> logger) : BackgroundService
{
    private const string ImaxApiBaseUrl = "https://imax.kagamine-rin.com";
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[IMAX_CHECK] IMAX notification check service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllNotificationsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_CHECK] Error during IMAX notification check cycle");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("[IMAX_CHECK] IMAX notification check service stopped");
    }

    private async Task CheckAllNotificationsAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var imaxNotificationService = scope.ServiceProvider.GetRequiredService<IImaxNotificationService>();

        // Cleanup expired notifications first
        var expiredCount = await imaxNotificationService.CleanupExpiredNotificationsAsync();
        if (expiredCount > 0)
            logger.LogInformation("[IMAX_CHECK] Cleaned up {Count} expired IMAX notifications", expiredCount);

        var notifications = await imaxNotificationService.GetAllActiveNotificationsAsync();
        if (notifications.Count == 0)
            return;

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[IMAX_CHECK] Checking {Count} active IMAX notifications", notifications.Count);

        // Group by date to avoid redundant API calls for the same date
        var dateGroups = notifications.GroupBy(n => n.ScreeningDate);
        var httpClient = httpClientFactory.CreateClient();

        foreach (var group in dateGroups)
        {
            try
            {
                var apiResult = await FetchImaxScheduleAsync(httpClient, group.Key, stoppingToken);
                if (apiResult is null)
                    continue;

                if (!apiResult.Value.GetProperty("hasImax").GetBoolean())
                    continue;

                // IMAX detected! Set pending message for all notifications with this date
                foreach (var notification in group)
                {
                    var message = FormatImaxMessage(notification, apiResult.Value);
                    await imaxNotificationService.SetPendingMessageAsync(notification.Id, message);

                    logger.LogInformation("[IMAX_CHECK] IMAX detected for room {RoomName}, date {Date}",
                        notification.RoomName, notification.ScreeningDate);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_CHECK] Error checking IMAX for date {Date}", group.Key);
            }
        }
    }

    private async Task<JsonElement?> FetchImaxScheduleAsync(HttpClient httpClient, string date, CancellationToken stoppingToken)
    {
        var url = $"{ImaxApiBaseUrl}/schedule?date={date}";
        var response = await httpClient.GetAsync(url, stoppingToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[IMAX_CHECK] IMAX API returned HTTP {StatusCode} for date {Date}",
                (int)response.StatusCode, date);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(stoppingToken);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static string FormatImaxMessage(Models.ImaxNotification notification, JsonElement apiResponse)
    {
        var dateDisplay = ImaxNotificationService.FormatScreeningDate(notification.ScreeningDate);
        var keywordSuffix = string.IsNullOrEmpty(notification.Keyword) ? "" : $" (키워드: {notification.Keyword})";

        var builder = new StringBuilder();
        builder.AppendLine($"🎬 용아맥 알림{keywordSuffix}");
        builder.AppendLine();

        var screenings = apiResponse.GetProperty("screenings");
        var imaxScreenings = screenings.EnumerateArray()
            .Where(s => s.GetProperty("isImax").GetBoolean())
            .ToList();

        builder.AppendLine($"📅 {dateDisplay} IMAX {imaxScreenings.Count}회차 감지!");
        builder.AppendLine();

        foreach (var screening in imaxScreenings)
        {
            var startTime = screening.GetProperty("startTime").GetString();
            var endTime = screening.GetProperty("endTime").GetString();
            var format = screening.GetProperty("format").GetString();
            var freeSeats = screening.GetProperty("freeSeats").GetInt32();
            var totalSeats = screening.GetProperty("totalSeats").GetInt32();
            builder.AppendLine($"🎬 {startTime}~{endTime} | {format} | 잔여 {freeSeats}/{totalSeats}석");
        }

        return builder.ToString().TrimEnd();
    }
}
