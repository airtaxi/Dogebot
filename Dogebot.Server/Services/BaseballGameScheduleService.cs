using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class BaseballGameScheduleService(IHttpClientFactory httpClientFactory, ILogger<BaseballGameScheduleService> logger) : IBaseballGameScheduleService
{
    private const string BaseballGameListApiAddress = "https://issue.daum.net/api/arms/SPORTS_GAME_LIST";
    private const string BaseballGameDetailApiAddress = "https://issue.daum.net/api/arms/SPORTS_GAME";
    private const string DaumSportsUserAgentValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0";
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(30);
    private static readonly Lock s_gameSnapshotCacheLock = new();
    private static readonly Lock s_gameDetailCacheLock = new();
    private static readonly Lock s_errorStateLock = new();
    private static readonly Dictionary<DateOnly, CachedBaseballGameScheduleSnapshot> s_cachedGameSnapshots = [];
    private static readonly Dictionary<BaseballGameDetailCacheKey, CachedBaseballGameDetail> s_cachedGameDetails = [];
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private static string? s_lastGameScheduleErrorDetails;

    private readonly HttpClient _baseballGameScheduleClient = httpClientFactory.CreateClient();

    public Task<BaseballGameScheduleSnapshot?> GetTodayGameSnapshotAsync() =>
        GetGameSnapshotAsync(GetTodayKoreanDate(), "오늘");

    public Task<BaseballGameDetail?> GetTodayGameDetailAsync(long gameId) =>
        GetGameDetailAsync(gameId, GetTodayKoreanDate(), "오늘");

    public Task<BaseballGameScheduleSnapshot?> GetTomorrowGameSnapshotAsync() =>
        GetGameSnapshotAsync(GetTodayKoreanDate().AddDays(1), "내일");

    public Task<BaseballGameDetail?> GetTomorrowGameDetailAsync(long gameId) =>
        GetGameDetailAsync(gameId, GetTodayKoreanDate().AddDays(1), "내일");

    public string? GetLastGameScheduleErrorDetails()
    {
        lock (s_errorStateLock)
        {
            return s_lastGameScheduleErrorDetails;
        }
    }

    private async Task<BaseballGameScheduleSnapshot?> GetGameSnapshotAsync(DateOnly targetDate, string dayLabel)
    {
        lock (s_gameSnapshotCacheLock)
        {
            if (s_cachedGameSnapshots.TryGetValue(targetDate, out var cachedGameSnapshot) &&
                DateTimeOffset.UtcNow - cachedGameSnapshot.CacheTime < s_cacheDuration)
                return cachedGameSnapshot.GameSnapshot;
        }

        try
        {
            var dateText = targetDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var requestAddress = $"{BaseballGameListApiAddress}?leagueCode=kbo&seasonKey={targetDate.Year}&fromDate={dateText}&toDate={dateText}&detail=true";
            var responseContent = await FetchApiContentAsync(requestAddress);
            if (string.IsNullOrWhiteSpace(responseContent)) return null;

            var responsePayload = JsonSerializer.Deserialize<BaseballGameListResponsePayload>(responseContent, s_jsonSerializerOptions);
            if (responsePayload?.Document?.Games == null || responsePayload.ResponseCode != 200)
            {
                SetLastGameScheduleErrorDetails(
                    $"{dayLabel} KBO 경기 목록 응답 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"RequestAddress: {requestAddress}\nResponsePreview:\n{BuildContentPreview(responseContent, 1000)}");
                logger.LogError("[BASEBALL_SCHEDULE] Failed to parse {DayLabel} game list response", dayLabel);
                return null;
            }

            var gameSummaries = responsePayload.Document.Games
                .Select(MapGameSummary)
                .ToList();
            var gameSnapshot = new BaseballGameScheduleSnapshot(targetDate, gameSummaries);

            lock (s_gameSnapshotCacheLock)
            {
                s_cachedGameSnapshots[targetDate] = new CachedBaseballGameScheduleSnapshot(gameSnapshot, DateTimeOffset.UtcNow);
            }

            ClearLastGameScheduleErrorDetails();
            return gameSnapshot;
        }
        catch (Exception exception)
        {
            SetLastGameScheduleErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_SCHEDULE] Error fetching {DayLabel} game list", dayLabel);
            return null;
        }
    }

    private async Task<BaseballGameDetail?> GetGameDetailAsync(long gameId, DateOnly targetDate, string dayLabel)
    {
        var cacheKey = new BaseballGameDetailCacheKey(targetDate, gameId);

        lock (s_gameDetailCacheLock)
        {
            if (s_cachedGameDetails.TryGetValue(cacheKey, out var cachedGameDetail) &&
                DateTimeOffset.UtcNow - cachedGameDetail.CacheTime < s_cacheDuration)
                return cachedGameDetail.GameDetail;
        }

        try
        {
            var requestAddress = $"{BaseballGameDetailApiAddress}?gameId={gameId}&detail=liveData,spPitchData,lineup";
            var responseContent = await FetchApiContentAsync(requestAddress);
            if (string.IsNullOrWhiteSpace(responseContent)) return null;

            var responsePayload = JsonSerializer.Deserialize<BaseballGameDetailResponsePayload>(responseContent, s_jsonSerializerOptions);
            if (responsePayload?.Game == null || responsePayload.ResponseCode != 200)
            {
                SetLastGameScheduleErrorDetails(
                    $"{dayLabel} KBO 경기 상세 응답 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"RequestAddress: {requestAddress}\nResponsePreview:\n{BuildContentPreview(responseContent, 1000)}");
                logger.LogError("[BASEBALL_SCHEDULE] Failed to parse {DayLabel} game detail response for {GameId}", dayLabel, gameId);
                return null;
            }

            var gameDetail = MapGameDetail(responsePayload.Game);
            lock (s_gameDetailCacheLock)
            {
                s_cachedGameDetails[cacheKey] = new CachedBaseballGameDetail(gameDetail, DateTimeOffset.UtcNow);
            }

            ClearLastGameScheduleErrorDetails();
            return gameDetail;
        }
        catch (Exception exception)
        {
            SetLastGameScheduleErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_SCHEDULE] Error fetching {DayLabel} game detail for {GameId}", dayLabel, gameId);
            return null;
        }
    }

    private async Task<string?> FetchApiContentAsync(string requestAddress)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestAddress);
        ConfigureDaumSportsRequestHeaders(requestMessage);

        using var responseMessage = await _baseballGameScheduleClient.SendAsync(requestMessage);
        var responseContent = await responseMessage.Content.ReadAsStringAsync();
        if (responseMessage.IsSuccessStatusCode) return responseContent;

        SetLastGameScheduleErrorDetails(
            $"HTTP 요청이 실패했습니다. StatusCode={(int)responseMessage.StatusCode} ({responseMessage.StatusCode})",
            Environment.StackTrace,
            $"RequestAddress: {requestAddress}\nResponsePreview:\n{BuildContentPreview(responseContent, 1000)}");
        logger.LogError("[BASEBALL_SCHEDULE] API request failed with status code {StatusCode}", responseMessage.StatusCode);
        return null;
    }

    private static void ConfigureDaumSportsRequestHeaders(HttpRequestMessage requestMessage)
    {
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.UserAgent.Clear();
        requestMessage.Headers.UserAgent.ParseAdd(DaumSportsUserAgentValue);
    }

    private static BaseballGameDetail MapGameDetail(BaseballGamePayload gamePayload)
    {
        var homeStartingPitcher = MapPlayer(gamePayload.HomeStartingPitcher);
        var awayStartingPitcher = MapPlayer(gamePayload.AwayStartingPitcher);
        var homePlayers = MapPlayers(gamePayload.HomePlayers);
        var awayPlayers = MapPlayers(gamePayload.AwayPlayers);

        return new BaseballGameDetail(
            MapGameSummary(gamePayload),
            homeStartingPitcher,
            awayStartingPitcher,
            homePlayers,
            awayPlayers,
            MapLiveData(gamePayload.LiveData));
    }

    private static BaseballGameScheduleSummary MapGameSummary(BaseballGamePayload gamePayload) =>
        new(
            gamePayload.GameId,
            gamePayload.GameStatus ?? string.Empty,
            gamePayload.PeriodType ?? string.Empty,
            gamePayload.LiveData?.Ground?.LastPeriod ?? string.Empty,
            gamePayload.StartDate ?? string.Empty,
            gamePayload.StartTime ?? string.Empty,
            MapParticipant(gamePayload.HomeParticipant),
            MapParticipant(gamePayload.AwayParticipant),
            MapScore(gamePayload.HomeScore),
            MapScore(gamePayload.AwayScore),
            MapField(gamePayload.Field));

    private static BaseballGameParticipant MapParticipant(BaseballParticipantPayload? participantPayload) =>
        new(participantPayload?.Result ?? string.Empty, MapTeam(participantPayload?.Team));

    private static BaseballGameTeam MapTeam(BaseballTeamPayload? teamPayload) =>
        new(
            teamPayload?.ProviderTeamId ?? string.Empty,
            GetFirstText(teamPayload?.NameKo, teamPayload?.Name, teamPayload?.NameMain, teamPayload?.ShortNameKo, teamPayload?.ShortName),
            GetFirstText(teamPayload?.ShortNameKo, teamPayload?.ShortName, teamPayload?.NameKo, teamPayload?.Name, teamPayload?.NameMain));

    private static BaseballGameScore? MapScore(BaseballScorePayload? scorePayload) =>
        scorePayload == null ? null : new BaseballGameScore(scorePayload.Run, scorePayload.Hit, scorePayload.Error, scorePayload.Walks);

    private static BaseballGameField? MapField(BaseballFieldPayload? fieldPayload)
    {
        if (fieldPayload == null) return null;

        var fieldName = GetFirstText(fieldPayload.NameKo, fieldPayload.Name, fieldPayload.NameMain, fieldPayload.ShortNameKo, fieldPayload.ShortName);
        var shortFieldName = GetFirstText(fieldPayload.ShortNameKo, fieldPayload.ShortName, fieldName);
        return new BaseballGameField(fieldName, shortFieldName);
    }

    private static IReadOnlyList<BaseballGamePlayer> MapPlayers(IReadOnlyList<BaseballPersonPayload>? personPayloads) =>
        personPayloads?
            .Select(MapPlayer)
            .OfType<BaseballGamePlayer>()
            .ToList() ?? [];

    private static BaseballGamePlayer? MapPlayer(BaseballPersonPayload? personPayload)
    {
        if (personPayload == null) return null;

        var providerPersonId = GetFirstText(personPayload.ProviderPersonId, personPayload.PersonId?.ToString(CultureInfo.InvariantCulture));
        var playerName = GetFirstText(personPayload.NameKo, personPayload.Name, personPayload.NameMain, personPayload.LastNameKo, personPayload.LastName);
        var positionName = GetFirstText(personPayload.PositionNameKo, personPayload.PositionName);
        if (string.IsNullOrWhiteSpace(providerPersonId) && string.IsNullOrWhiteSpace(playerName)) return null;

        return new BaseballGamePlayer(providerPersonId, playerName, personPayload.BattingOrder, positionName);
    }

    private static BaseballGameLiveData? MapLiveData(BaseballLiveDataPayload? liveDataPayload)
    {
        if (liveDataPayload == null) return null;

        var liveEvents = liveDataPayload.LiveEvents?
            .Select(MapLiveEvent)
            .Where(liveEvent => !string.IsNullOrWhiteSpace(liveEvent.Text))
            .ToList() ?? [];
        return new BaseballGameLiveData(MapGround(liveDataPayload.Ground), liveEvents);
    }

    private static BaseballGameGround? MapGround(BaseballGroundPayload? groundPayload)
    {
        if (groundPayload == null) return null;

        return new BaseballGameGround(
            groundPayload.Ball,
            groundPayload.Strike,
            groundPayload.Out,
            groundPayload.BaseFirstRunnerProviderPersonId ?? string.Empty,
            groundPayload.BaseSecondRunnerProviderPersonId ?? string.Empty,
            groundPayload.BaseThirdRunnerProviderPersonId ?? string.Empty,
            groundPayload.LastPeriod ?? string.Empty);
    }

    private static BaseballGameLiveEvent MapLiveEvent(BaseballLiveEventPayload liveEventPayload) =>
        new(
            liveEventPayload.Period ?? string.Empty,
            liveEventPayload.BatterProviderPersonId ?? string.Empty,
            liveEventPayload.BallCount,
            liveEventPayload.Ball,
            liveEventPayload.Strike,
            liveEventPayload.Speed,
            liveEventPayload.PitcherProviderPersonId ?? string.Empty,
            liveEventPayload.Text ?? string.Empty,
            liveEventPayload.PitchKind ?? string.Empty);

    private static DateOnly GetTodayKoreanDate()
    {
        var koreanStandardTimeZoneInfo = GetKoreanStandardTimeZoneInfo();
        var currentKoreanDateTimeOffset = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, koreanStandardTimeZoneInfo);
        return DateOnly.FromDateTime(currentKoreanDateTimeOffset.DateTime);
    }

    private static TimeZoneInfo GetKoreanStandardTimeZoneInfo()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
    }

    private static void ClearLastGameScheduleErrorDetails()
    {
        lock (s_errorStateLock)
        {
            s_lastGameScheduleErrorDetails = null;
        }
    }

    private static void SetLastGameScheduleErrorDetails(string message, string? stackTrace, string? additionalInformation) =>
        SetLastGameScheduleErrorDetails(BuildErrorDetails(message, stackTrace, additionalInformation));

    private static void SetLastGameScheduleErrorDetails(string errorDetails)
    {
        lock (s_errorStateLock)
        {
            s_lastGameScheduleErrorDetails = errorDetails;
        }
    }

    private static string BuildErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message) ? "알 수 없는 오류" : message;
        var errorStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? "스택 추적 정보 없음" : stackTrace;
        var errorAdditionalInformation = string.IsNullOrWhiteSpace(additionalInformation) ? "추가 정보 없음" : additionalInformation;
        return $"Message: {errorMessage}\nStackTrace: {errorStackTrace}\nAdditionalInfo: {errorAdditionalInformation}";
    }

    private static string BuildContentPreview(string content, int maximumLength) =>
        content.Length <= maximumLength ? content : $"{content[..maximumLength]}...";

    private static string GetFirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record CachedBaseballGameScheduleSnapshot(BaseballGameScheduleSnapshot GameSnapshot, DateTimeOffset CacheTime);

    private sealed record CachedBaseballGameDetail(BaseballGameDetail GameDetail, DateTimeOffset CacheTime);

    private sealed record BaseballGameDetailCacheKey(DateOnly GameDate, long GameId);

    private sealed record BaseballGameListResponsePayload(
        [property: JsonPropertyName("code")] int ResponseCode,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("document")] BaseballGameListDocumentPayload? Document);

    private sealed record BaseballGameListDocumentPayload(
        [property: JsonPropertyName("list")] IReadOnlyList<BaseballGamePayload>? Games);

    private sealed record BaseballGameDetailResponsePayload(
        [property: JsonPropertyName("code")] int ResponseCode,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("document")] BaseballGamePayload? Game);

    private sealed record BaseballGamePayload(
        [property: JsonPropertyName("gameId")] long GameId,
        [property: JsonPropertyName("startDate")] string? StartDate,
        [property: JsonPropertyName("startTime")] string? StartTime,
        [property: JsonPropertyName("periodType")] string? PeriodType,
        [property: JsonPropertyName("gameStatus")] string? GameStatus,
        [property: JsonPropertyName("home")] BaseballParticipantPayload? HomeParticipant,
        [property: JsonPropertyName("away")] BaseballParticipantPayload? AwayParticipant,
        [property: JsonPropertyName("homeScore")] BaseballScorePayload? HomeScore,
        [property: JsonPropertyName("awayScore")] BaseballScorePayload? AwayScore,
        [property: JsonPropertyName("field")] BaseballFieldPayload? Field,
        [property: JsonPropertyName("homeStartPitcher")] BaseballPersonPayload? HomeStartingPitcher,
        [property: JsonPropertyName("awayStartPitcher")] BaseballPersonPayload? AwayStartingPitcher,
        [property: JsonPropertyName("homePerson")] IReadOnlyList<BaseballPersonPayload>? HomePlayers,
        [property: JsonPropertyName("awayPerson")] IReadOnlyList<BaseballPersonPayload>? AwayPlayers,
        [property: JsonPropertyName("liveData")] BaseballLiveDataPayload? LiveData);

    private sealed record BaseballParticipantPayload(
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("team")] BaseballTeamPayload? Team);

    private sealed record BaseballTeamPayload(
        [property: JsonPropertyName("cpTeamId")] string? ProviderTeamId,
        [property: JsonPropertyName("nameMain")] string? NameMain,
        [property: JsonPropertyName("nameKo")] string? NameKo,
        [property: JsonPropertyName("shortNameKo")] string? ShortNameKo,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("shortName")] string? ShortName);

    private sealed record BaseballScorePayload(
        [property: JsonPropertyName("run")] int? Run,
        [property: JsonPropertyName("hit")] int? Hit,
        [property: JsonPropertyName("error")] int? Error,
        [property: JsonPropertyName("ballfour")] int? Walks);

    private sealed record BaseballFieldPayload(
        [property: JsonPropertyName("nameMain")] string? NameMain,
        [property: JsonPropertyName("nameKo")] string? NameKo,
        [property: JsonPropertyName("shortNameKo")] string? ShortNameKo,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("shortName")] string? ShortName);

    private sealed record BaseballPersonPayload(
        [property: JsonPropertyName("personId")] long? PersonId,
        [property: JsonPropertyName("cpPersonId")] string? ProviderPersonId,
        [property: JsonPropertyName("battingOrder")] int? BattingOrder,
        [property: JsonPropertyName("nameMain")] string? NameMain,
        [property: JsonPropertyName("nameKo")] string? NameKo,
        [property: JsonPropertyName("lastNameKo")] string? LastNameKo,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("positionName")] string? PositionName,
        [property: JsonPropertyName("positionNameKo")] string? PositionNameKo);

    private sealed record BaseballLiveDataPayload(
        [property: JsonPropertyName("ground")] BaseballGroundPayload? Ground,
        [property: JsonPropertyName("liveText")] IReadOnlyList<BaseballLiveEventPayload>? LiveEvents);

    private sealed record BaseballGroundPayload(
        [property: JsonPropertyName("ball")] int? Ball,
        [property: JsonPropertyName("strike")] int? Strike,
        [property: JsonPropertyName("out")] int? Out,
        [property: JsonPropertyName("base1")] string? BaseFirstRunnerProviderPersonId,
        [property: JsonPropertyName("base2")] string? BaseSecondRunnerProviderPersonId,
        [property: JsonPropertyName("base3")] string? BaseThirdRunnerProviderPersonId,
        [property: JsonPropertyName("lastPeriod")] string? LastPeriod);

    private sealed record BaseballLiveEventPayload(
        [property: JsonPropertyName("period")] string? Period,
        [property: JsonPropertyName("batter")] string? BatterProviderPersonId,
        [property: JsonPropertyName("ballCount")] int? BallCount,
        [property: JsonPropertyName("ball")] int? Ball,
        [property: JsonPropertyName("strike")] int? Strike,
        [property: JsonPropertyName("speed")] int? Speed,
        [property: JsonPropertyName("pitcher")] string? PitcherProviderPersonId,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("stuff")] string? PitchKind);
}
