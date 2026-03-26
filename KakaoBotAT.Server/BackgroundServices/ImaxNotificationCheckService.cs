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
    private static readonly Random CheckIntervalRandom = new();

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
                var delaySeconds = CheckIntervalRandom.Next(30, 61);
                logger.LogDebug("[IMAX_CHECK] Next check in {Seconds} seconds", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
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

        // Group by (date, movieNumber) to avoid redundant API calls
        var dateMovieGroups = notifications.GroupBy(n => (n.ScreeningDate, n.MovieNumber));
        var httpClient = httpClientFactory.CreateClient();

        foreach (var group in dateMovieGroups)
        {
            try
            {
                var apiResult = await FetchImaxScheduleAsync(httpClient, group.Key.ScreeningDate, group.Key.MovieNumber, stoppingToken);
                if (apiResult is null)
                    continue;

                if (!apiResult.Value.GetProperty("hasImax").GetBoolean())
                    continue;

                // IMAX detected! Set pending message for all notifications with this date+movie
                foreach (var notification in group)
                {
                    var message = FormatImaxMessage(notification, apiResult.Value);
                    await imaxNotificationService.SetPendingMessageAsync(notification.Id, message);

                    logger.LogInformation("[IMAX_CHECK] IMAX detected for room {RoomName}, movie {Movie}, date {Date}",
                        notification.RoomName, notification.MovieName, notification.ScreeningDate);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_CHECK] Error checking IMAX for date {Date}", group.Key);
            }
        }
    }

    private async Task<JsonElement?> FetchImaxScheduleAsync(HttpClient httpClient, string date, string movieNumber, CancellationToken stoppingToken)
    {
        var url = $"{ImaxApiBaseUrl}/schedule?date={date}&movNo={movieNumber}";
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
        builder.AppendLine($"🎬 {notification.MovieName}");

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
