using System.Globalization;
using Dogebot.Server.Baseball;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.BackgroundServices;

public class BaseballGameSubscriptionCheckService(
    IServiceProvider serviceProvider,
    ILogger<BaseballGameSubscriptionCheckService> logger) : BackgroundService
{
    private static readonly TimeSpan s_checkInterval = TimeSpan.FromSeconds(30);
    private const int MaximumEventsPerMessage = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[BASEBALL_SUBSCRIPTION_CHECK] Baseball game subscription check service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckActiveSubscriptionsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[BASEBALL_SUBSCRIPTION_CHECK] Error during baseball subscription check cycle");
            }

            try
            {
                await Task.Delay(s_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("[BASEBALL_SUBSCRIPTION_CHECK] Baseball game subscription check service stopped");
    }

    private async Task CheckActiveSubscriptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var baseballGameSubscriptionService = scope.ServiceProvider.GetRequiredService<IBaseballGameSubscriptionService>();
        var baseballGameScheduleService = scope.ServiceProvider.GetRequiredService<IBaseballGameScheduleService>();
        var activeSubscriptions = await baseballGameSubscriptionService.GetActiveSubscriptionsAsync();
        if (activeSubscriptions.Count == 0) return;

        var subscriptionGroups = activeSubscriptions.GroupBy(subscription =>
            new BaseballGameSubscriptionCheckKey(subscription.GameDate, subscription.GameId));

        foreach (var subscriptionGroup in subscriptionGroups)
        {
            stoppingToken.ThrowIfCancellationRequested();
            if (!DateOnly.TryParseExact(
                    subscriptionGroup.Key.GameDate,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var gameDate))
            {
                logger.LogWarning("[BASEBALL_SUBSCRIPTION_CHECK] Invalid game date {GameDate}", subscriptionGroup.Key.GameDate);
                continue;
            }

            var gameDetail = await baseballGameScheduleService.GetGameDetailAsync(gameDate, subscriptionGroup.Key.GameId);
            if (gameDetail == null)
            {
                logger.LogWarning(
                    "[BASEBALL_SUBSCRIPTION_CHECK] Failed to fetch subscribed baseball game detail {GameDate}/{GameId}",
                    subscriptionGroup.Key.GameDate,
                    subscriptionGroup.Key.GameId);
                continue;
            }

            foreach (var subscription in subscriptionGroup)
            {
                var checkResult = BuildCheckResult(subscription, gameDetail);
                await baseballGameSubscriptionService.ApplyCheckResultAsync(subscription, checkResult);
            }
        }
    }

    private static BaseballGameSubscriptionCheckResult BuildCheckResult(BaseballGameSubscription subscription, BaseballGameDetail gameDetail)
    {
        var pendingMessages = new List<string>();
        if (BaseballGameFormatter.IsRainCanceledGame(gameDetail.GameSummary))
        {
            pendingMessages.Add(BaseballGameFormatter.FormatRainCanceledNotification(gameDetail));
            return new BaseballGameSubscriptionCheckResult(
                subscription.LastDeliveredLiveEventKey,
                subscription.LastDeliveredLiveEventIndex,
                gameDetail.GameSummary.HomeScore?.Run,
                gameDetail.GameSummary.AwayScore?.Run,
                subscription.LineupNotified,
                BaseballGameSubscriptionStatus.Completed,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pendingMessages);
        }

        var liveEvents = BaseballGameFormatter.GetLiveGameEvents(gameDetail).ToList();
        var previousLiveEventIndex = ResolveLastDeliveredLiveEventIndex(subscription, liveEvents);
        var newLiveEvents = previousLiveEventIndex < liveEvents.Count - 1
            ? liveEvents.Skip(previousLiveEventIndex + 1).ToList()
            : [];
        var lastDeliveredLiveEventIndex = subscription.LastDeliveredLiveEventIndex;
        var lastDeliveredLiveEventKey = subscription.LastDeliveredLiveEventKey;
        var lineupNotified = subscription.LineupNotified;

        if (!lineupNotified && BaseballGameFormatter.HasCompleteLineups(gameDetail))
        {
            pendingMessages.Add(BaseballGameFormatter.FormatLineupConfirmedNotification(gameDetail));
            lineupNotified = true;
        }

        var currentHomeScore = gameDetail.GameSummary.HomeScore?.Run;
        var currentAwayScore = gameDetail.GameSummary.AwayScore?.Run;
        var hasPreviousScore = subscription.LastHomeScore.HasValue || subscription.LastAwayScore.HasValue;
        var hasCurrentScore = currentHomeScore.HasValue || currentAwayScore.HasValue;
        var scoreChanged = hasPreviousScore &&
                           hasCurrentScore &&
                           (subscription.LastHomeScore != currentHomeScore || subscription.LastAwayScore != currentAwayScore);

        if (scoreChanged)
        {
            if (newLiveEvents.Count > 0)
            {
                lastDeliveredLiveEventIndex = liveEvents.Count - 1;
                lastDeliveredLiveEventKey = BaseballGameFormatter.BuildLiveEventKey(liveEvents[^1]);

                var omittedEventCount = Math.Max(0, newLiveEvents.Count - MaximumEventsPerMessage);
                var displayedLiveEvents = newLiveEvents
                    .TakeLast(MaximumEventsPerMessage)
                    .Reverse()
                    .ToList();
                pendingMessages.Add(BaseballGameFormatter.FormatScoreChangedWithEventsNotification(
                    gameDetail,
                    subscription.LastHomeScore,
                    subscription.LastAwayScore,
                    displayedLiveEvents,
                    omittedEventCount));
            }
            else
            {
                pendingMessages.Add(BaseballGameFormatter.FormatScoreChangedNotification(
                    gameDetail,
                    subscription.LastHomeScore,
                    subscription.LastAwayScore));
            }
        }

        var status = BaseballGameSubscriptionStatus.Active;
        long? completedAt = null;
        if (BaseballGameFormatter.IsFinishedOrUnavailableGame(gameDetail.GameSummary))
        {
            status = BaseballGameSubscriptionStatus.Completed;
            completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        return new BaseballGameSubscriptionCheckResult(
            lastDeliveredLiveEventKey,
            lastDeliveredLiveEventIndex,
            currentHomeScore,
            currentAwayScore,
            lineupNotified,
            status,
            completedAt,
            pendingMessages);
    }

    private static int ResolveLastDeliveredLiveEventIndex(
        BaseballGameSubscription subscription,
        IReadOnlyList<BaseballGameLiveEvent> liveEvents)
    {
        if (liveEvents.Count == 0) return -1;
        var cachedLiveEventIndexMatches = subscription.LastDeliveredLiveEventIndex >= 0 &&
                                          subscription.LastDeliveredLiveEventIndex < liveEvents.Count &&
                                          BaseballGameFormatter.BuildLiveEventKey(liveEvents[subscription.LastDeliveredLiveEventIndex])
                                              .Equals(subscription.LastDeliveredLiveEventKey, StringComparison.Ordinal);
        if (cachedLiveEventIndexMatches) return subscription.LastDeliveredLiveEventIndex;

        if (!string.IsNullOrWhiteSpace(subscription.LastDeliveredLiveEventKey))
        {
            for (var liveEventIndex = liveEvents.Count - 1; liveEventIndex >= 0; liveEventIndex--)
            {
                var liveEventKey = BaseballGameFormatter.BuildLiveEventKey(liveEvents[liveEventIndex]);
                if (liveEventKey.Equals(subscription.LastDeliveredLiveEventKey, StringComparison.Ordinal)) return liveEventIndex;
            }
        }

        if (subscription.LastDeliveredLiveEventIndex >= 0) return Math.Min(subscription.LastDeliveredLiveEventIndex, liveEvents.Count - 1);

        return -1;
    }

    private sealed record BaseballGameSubscriptionCheckKey(string GameDate, long GameId);
}
