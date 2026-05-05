using Dogebot.Commons;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IBaseballGameSubscriptionService
{
    Task<BaseballGameSubscriptionRegisterResult> RegisterAsync(
        KakaoMessageData data,
        DateOnly gameDate,
        BaseballGameDetail gameDetail,
        string subscribedTeamName);

    Task<BaseballGameSubscriptionRemoveResult> RemoveAsync(string roomId, string teamSearchText);

    Task<List<BaseballGameSubscription>> GetActiveSubscriptionsAsync();

    Task ApplyCheckResultAsync(BaseballGameSubscription subscription, BaseballGameSubscriptionCheckResult checkResult);

    Task<ServerResponse?> CheckAndDeliverAsync(KakaoMessageData data);

    Task<ServerResponse?> CheckAndDeliverForRoomsAsync(IEnumerable<string> roomIds);
}

public sealed record BaseballGameSubscriptionRegisterResult(
    bool Success,
    bool AlreadySubscribed,
    string Message,
    BaseballGameSubscription? Subscription);

public sealed record BaseballGameSubscriptionRemoveResult(
    int RemovedCount,
    string Message);

public sealed record BaseballGameSubscriptionCheckResult(
    string LastDeliveredLiveEventKey,
    int LastDeliveredLiveEventIndex,
    int? LastHomeScore,
    int? LastAwayScore,
    bool LineupNotified,
    BaseballGameSubscriptionStatus Status,
    long? CompletedAt,
    IReadOnlyList<string> PendingMessages);
