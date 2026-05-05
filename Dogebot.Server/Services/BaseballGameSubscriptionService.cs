using System.Globalization;
using Dogebot.Commons;
using Dogebot.Server.Baseball;
using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class BaseballGameSubscriptionService : IBaseballGameSubscriptionService
{
    private readonly IMongoCollection<BaseballGameSubscription> _subscriptions;
    private readonly IMongoCollection<BaseballGameSubscriptionMessage> _messages;

    public BaseballGameSubscriptionService(IMongoDbService mongoDbService)
    {
        _subscriptions = mongoDbService.Database.GetCollection<BaseballGameSubscription>("baseballGameSubscriptions");
        _messages = mongoDbService.Database.GetCollection<BaseballGameSubscriptionMessage>("baseballGameSubscriptionMessages");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var subscriptionIndexes = new[]
        {
            new CreateIndexModel<BaseballGameSubscription>(
                Builders<BaseballGameSubscription>.IndexKeys
                    .Ascending(subscription => subscription.RoomId)
                    .Ascending(subscription => subscription.GameDate)
                    .Ascending(subscription => subscription.GameId)
                    .Ascending(subscription => subscription.Status)),
            new CreateIndexModel<BaseballGameSubscription>(
                Builders<BaseballGameSubscription>.IndexKeys
                    .Ascending(subscription => subscription.Status)
                    .Ascending(subscription => subscription.GameDate)
                    .Ascending(subscription => subscription.GameId))
        };
        _subscriptions.Indexes.CreateMany(subscriptionIndexes);

        var messageIndexes = new[]
        {
            new CreateIndexModel<BaseballGameSubscriptionMessage>(
                Builders<BaseballGameSubscriptionMessage>.IndexKeys
                    .Ascending(message => message.RoomId)
                    .Ascending(message => message.CreatedAt)),
            new CreateIndexModel<BaseballGameSubscriptionMessage>(
                Builders<BaseballGameSubscriptionMessage>.IndexKeys
                    .Ascending(message => message.SubscriptionId))
        };
        _messages.Indexes.CreateMany(messageIndexes);
    }

    public async Task<BaseballGameSubscriptionRegisterResult> RegisterAsync(
        KakaoMessageData data,
        DateOnly gameDate,
        BaseballGameDetail gameDetail,
        string subscribedTeamName)
    {
        var gameSummary = gameDetail.GameSummary;
        var gameDateText = gameDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var activeSubscriptionFilter = Builders<BaseballGameSubscription>.Filter.And(
            Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.RoomId, data.RoomId),
            Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.GameDate, gameDateText),
            Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.GameId, gameSummary.GameId),
            Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.Status, BaseballGameSubscriptionStatus.Active));

        var existingSubscription = await _subscriptions.Find(activeSubscriptionFilter).FirstOrDefaultAsync();
        if (existingSubscription is not null)
        {
            return new BaseballGameSubscriptionRegisterResult(
                false,
                true,
                "이미 경기를 구독중입니다.",
                existingSubscription);
        }

        var liveEvents = BaseballGameFormatter.GetLiveGameEvents(gameDetail);
        var lastLiveEventIndex = liveEvents.Count - 1;
        var lastLiveEventKey = lastLiveEventIndex >= 0
            ? BaseballGameFormatter.BuildLiveEventKey(liveEvents[lastLiveEventIndex])
            : string.Empty;
        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var subscription = new BaseballGameSubscription
        {
            RoomId = data.RoomId,
            RoomName = data.RoomName,
            SubscriptionMode = BaseballGameSubscriptionMode.OneGame,
            GameDate = gameDateText,
            GameId = gameSummary.GameId,
            HomeTeamProviderId = gameSummary.HomeParticipant.Team.ProviderTeamId,
            HomeTeamName = gameSummary.HomeParticipant.Team.Name,
            HomeTeamShortName = gameSummary.HomeParticipant.Team.ShortName,
            AwayTeamProviderId = gameSummary.AwayParticipant.Team.ProviderTeamId,
            AwayTeamName = gameSummary.AwayParticipant.Team.Name,
            AwayTeamShortName = gameSummary.AwayParticipant.Team.ShortName,
            SubscribedTeamName = subscribedTeamName,
            Status = BaseballGameSubscriptionStatus.Active,
            LastDeliveredLiveEventKey = lastLiveEventKey,
            LastDeliveredLiveEventIndex = lastLiveEventIndex,
            LastHomeScore = gameSummary.HomeScore?.Run,
            LastAwayScore = gameSummary.AwayScore?.Run,
            LineupNotified = BaseballGameFormatter.HasCompleteLineups(gameDetail),
            CreatedBy = data.SenderHash,
            CreatedByName = data.SenderName,
            CreatedAt = currentUnixTime,
            UpdatedAt = currentUnixTime
        };

        await _subscriptions.InsertOneAsync(subscription);

        var message = $"✅ 야구 경기 구독 완료\n\n" +
                      $"📅 {gameDate:yyyy-MM-dd}\n" +
                      $"⚾ {BaseballGameFormatter.FormatGameMatchDescription(gameSummary)}\n" +
                      $"📌 구독 팀: {subscribedTeamName}\n" +
                      $"상태: {BaseballGameFormatter.FormatGameStatus(gameSummary, gameDetail)}";

        return new BaseballGameSubscriptionRegisterResult(true, false, message, subscription);
    }

    public async Task<BaseballGameSubscriptionRemoveResult> RemoveAsync(string roomId, string teamSearchText)
    {
        var activeSubscriptions = await _subscriptions
            .Find(Builders<BaseballGameSubscription>.Filter.And(
                Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.RoomId, roomId),
                Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.Status, BaseballGameSubscriptionStatus.Active)))
            .ToListAsync();

        var matchingSubscriptions = activeSubscriptions
            .Where(subscription => MatchesSubscriptionTeam(subscription, teamSearchText))
            .ToList();
        if (matchingSubscriptions.Count == 0)
            return new BaseballGameSubscriptionRemoveResult(0, $"'{teamSearchText}' 팀의 야구 경기 구독이 없습니다.");

        var subscriptionIds = matchingSubscriptions.Select(subscription => subscription.Id).ToList();
        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subscriptionFilter = Builders<BaseballGameSubscription>.Filter.In(subscription => subscription.Id, subscriptionIds);
        var subscriptionUpdate = Builders<BaseballGameSubscription>.Update
            .Set(subscription => subscription.Status, BaseballGameSubscriptionStatus.Canceled)
            .Set(subscription => subscription.UpdatedAt, currentUnixTime)
            .Set(subscription => subscription.CompletedAt, currentUnixTime);
        var updateResult = await _subscriptions.UpdateManyAsync(subscriptionFilter, subscriptionUpdate);

        var messageFilter = Builders<BaseballGameSubscriptionMessage>.Filter.In(message => message.SubscriptionId, subscriptionIds);
        await _messages.DeleteManyAsync(messageFilter);

        var removedCount = (int)updateResult.ModifiedCount;
        return new BaseballGameSubscriptionRemoveResult(removedCount, $"✅ 야구 경기 구독 {removedCount}개를 해제했습니다.");
    }

    public async Task<List<BaseballGameSubscription>> GetActiveSubscriptionsAsync()
    {
        var filter = Builders<BaseballGameSubscription>.Filter.Eq(subscription => subscription.Status, BaseballGameSubscriptionStatus.Active);
        return await _subscriptions.Find(filter).ToListAsync();
    }

    public async Task ApplyCheckResultAsync(BaseballGameSubscription subscription, BaseballGameSubscriptionCheckResult checkResult)
    {
        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subscriptionFilter = Builders<BaseballGameSubscription>.Filter.And(
            Builders<BaseballGameSubscription>.Filter.Eq(existingSubscription => existingSubscription.Id, subscription.Id),
            Builders<BaseballGameSubscription>.Filter.Eq(existingSubscription => existingSubscription.Status, BaseballGameSubscriptionStatus.Active));
        var subscriptionUpdate = Builders<BaseballGameSubscription>.Update
            .Set(existingSubscription => existingSubscription.LastDeliveredLiveEventKey, checkResult.LastDeliveredLiveEventKey)
            .Set(existingSubscription => existingSubscription.LastDeliveredLiveEventIndex, checkResult.LastDeliveredLiveEventIndex)
            .Set(existingSubscription => existingSubscription.LastHomeScore, checkResult.LastHomeScore)
            .Set(existingSubscription => existingSubscription.LastAwayScore, checkResult.LastAwayScore)
            .Set(existingSubscription => existingSubscription.LineupNotified, checkResult.LineupNotified)
            .Set(existingSubscription => existingSubscription.Status, checkResult.Status)
            .Set(existingSubscription => existingSubscription.UpdatedAt, currentUnixTime)
            .Set(existingSubscription => existingSubscription.CompletedAt, checkResult.CompletedAt);

        var updateResult = await _subscriptions.UpdateOneAsync(subscriptionFilter, subscriptionUpdate);
        if (updateResult.MatchedCount == 0) return;
        if (checkResult.PendingMessages.Count == 0) return;

        var pendingMessages = checkResult.PendingMessages.Select(pendingMessage => new BaseballGameSubscriptionMessage
        {
            SubscriptionId = subscription.Id,
            RoomId = subscription.RoomId,
            GameDate = subscription.GameDate,
            GameId = subscription.GameId,
            Message = pendingMessage,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }).ToList();

        await _messages.InsertManyAsync(pendingMessages);
    }

    public async Task<ServerResponse?> CheckAndDeliverAsync(KakaoMessageData data)
    {
        var filter = Builders<BaseballGameSubscriptionMessage>.Filter.Eq(message => message.RoomId, data.RoomId);
        return await FindAndDeletePendingMessageAsync(filter);
    }

    public async Task<ServerResponse?> CheckAndDeliverForRoomsAsync(IEnumerable<string> roomIds)
    {
        var roomIdList = roomIds.ToList();
        if (roomIdList.Count == 0) return null;

        var filter = Builders<BaseballGameSubscriptionMessage>.Filter.In(message => message.RoomId, roomIdList);
        return await FindAndDeletePendingMessageAsync(filter);
    }

    private async Task<ServerResponse?> FindAndDeletePendingMessageAsync(FilterDefinition<BaseballGameSubscriptionMessage> filter)
    {
        var options = new FindOneAndDeleteOptions<BaseballGameSubscriptionMessage>
        {
            Sort = Builders<BaseballGameSubscriptionMessage>.Sort
                .Ascending(message => message.CreatedAt)
                .Ascending(message => message.Id)
        };
        var pendingMessage = await _messages.FindOneAndDeleteAsync(filter, options);
        if (pendingMessage is null) return null;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = pendingMessage.RoomId,
            Message = pendingMessage.Message
        };
    }

    private static bool MatchesSubscriptionTeam(BaseballGameSubscription subscription, string teamSearchText)
    {
        var homeTeam = new BaseballGameTeam(
            subscription.HomeTeamProviderId,
            subscription.HomeTeamName,
            subscription.HomeTeamShortName);
        var awayTeam = new BaseballGameTeam(
            subscription.AwayTeamProviderId,
            subscription.AwayTeamName,
            subscription.AwayTeamShortName);

        return BaseballGameFormatter.DoesTeamMatch(homeTeam, teamSearchText) ||
               BaseballGameFormatter.DoesTeamMatch(awayTeam, teamSearchText);
    }
}
