using System.Globalization;
using System.Text;
using Dogebot.Server.Models;

namespace Dogebot.Server.Baseball;

public static class BaseballGameFormatter
{
    private const string MessageSeparator = "━━━━━━━━━━━━━━━━━━";

    public static string FormatGameSummaryMessage(BaseballGameScheduleSnapshot gameSnapshot, string dayLabel)
    {
        var gameDateText = gameSnapshot.GameDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (gameSnapshot.GameSummaries.Count == 0) return $"⚾ {dayLabel} KBO 경기 ({gameDateText})\n\n{dayLabel} 예정된 KBO 경기가 없습니다.";

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ {dayLabel} KBO 경기 ({gameDateText})");
        stringBuilder.AppendLine();

        for (var gameIndex = 0; gameIndex < gameSnapshot.GameSummaries.Count; gameIndex++)
        {
            var gameSummary = gameSnapshot.GameSummaries[gameIndex];
            var awayTeamName = GetTeamDisplayName(gameSummary.AwayParticipant.Team);
            var homeTeamName = GetTeamDisplayName(gameSummary.HomeParticipant.Team);
            var awayScoreText = FormatParticipantScore(gameSummary.AwayParticipant, gameSummary.AwayScore);
            var homeScoreText = FormatParticipantScore(gameSummary.HomeParticipant, gameSummary.HomeScore);
            var gameStatusText = FormatGameStatus(gameSummary, null);
            var startTimeText = FormatStartTime(gameSummary.StartTime);
            stringBuilder.AppendLine($"{gameIndex + 1}. {awayTeamName} {awayScoreText} : {homeScoreText} {homeTeamName} / {gameStatusText} / {startTimeText}");
        }

        return stringBuilder.ToString().TrimEnd();
    }

    public static string FormatGameDetailMessage(BaseballGameDetail gameDetail)
    {
        var gameSummary = gameDetail.GameSummary;
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(FormatHomeAwayScoreLine(gameSummary));
        stringBuilder.AppendLine($"경기장: {FormatFieldName(gameSummary.Field)}");
        AppendCountAndBaseState(stringBuilder, gameDetail);
        AppendTeamScoreStatistics(stringBuilder, gameSummary);
        stringBuilder.AppendLine($"경기 상태: {FormatGameStatus(gameSummary, gameDetail)}");

        if (IsBeforeGame(gameSummary)) AppendBeforeGameInformation(stringBuilder, gameDetail);
        else if (IsEndedGame(gameSummary)) AppendAfterGameInformation(stringBuilder, gameDetail);
        else if (IsCanceledGame(gameSummary)) AppendCanceledGameInformation(stringBuilder, gameSummary);
        else AppendLiveGameInformation(stringBuilder, gameDetail);

        return stringBuilder.ToString().TrimEnd();
    }

    public static string FormatSubscriptionLiveEventNotification(
        BaseballGameDetail gameDetail,
        IReadOnlyList<BaseballGameLiveEvent> liveEvents,
        int omittedEventCount)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ {FormatGameMatchDescription(gameDetail.GameSummary)} 경기 이벤트");
        if (omittedEventCount > 0) stringBuilder.AppendLine($"이전 {omittedEventCount}개 이벤트는 표시하지 못했습니다.");
        stringBuilder.AppendLine();

        for (var liveEventIndex = 0; liveEventIndex < liveEvents.Count; liveEventIndex++)
        {
            if (liveEventIndex > 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(MessageSeparator);
                stringBuilder.AppendLine();
            }

            AppendLiveEventText(stringBuilder, gameDetail, liveEvents[liveEventIndex], includeDetails: true);
        }

        return stringBuilder.ToString().TrimEnd();
    }

    public static string FormatLineupConfirmedNotification(BaseballGameDetail gameDetail)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ {FormatGameMatchDescription(gameDetail.GameSummary)} 라인업 확정");
        AppendBeforeGameInformation(stringBuilder, gameDetail);
        return stringBuilder.ToString().TrimEnd();
    }

    public static string FormatScoreChangedNotification(BaseballGameDetail gameDetail, int? previousHomeScore, int? previousAwayScore)
    {
        var gameSummary = gameDetail.GameSummary;
        var homeTeamName = GetTeamDisplayName(gameSummary.HomeParticipant.Team);
        var awayTeamName = GetTeamDisplayName(gameSummary.AwayParticipant.Team);
        var previousScoreText = $"{homeTeamName} {FormatNullableNumber(previousHomeScore)} : {FormatNullableNumber(previousAwayScore)} {awayTeamName}";

        return $"⚾ {FormatGameMatchDescription(gameSummary)} 점수 변경\n\n" +
               $"이전: {previousScoreText}\n" +
               $"현재: {FormatHomeAwayScoreLine(gameSummary)}";
    }

    public static string FormatRainCanceledNotification(BaseballGameDetail gameDetail) =>
        $"⚾ {FormatGameMatchDescription(gameDetail.GameSummary)} 우천취소 안내\n\n" +
        $"{FormatFieldName(gameDetail.GameSummary.Field)} 경기가 우천취소되었습니다.\n" +
        "해당 경기 구독은 완료 처리됩니다.";

    public static List<BaseballGameScheduleSummary> FindMatchingGameSummaries(IReadOnlyList<BaseballGameScheduleSummary> gameSummaries, string teamSearchText)
    {
        var normalizedTeamSearchText = NormalizeTeamSearchText(teamSearchText);
        var gameMatches = gameSummaries
            .Select(gameSummary =>
            {
                var homeTeamMatch = GetTeamMatchResult(gameSummary.HomeParticipant.Team, normalizedTeamSearchText);
                var awayTeamMatch = GetTeamMatchResult(gameSummary.AwayParticipant.Team, normalizedTeamSearchText);
                return new
                {
                    GameSummary = gameSummary,
                    HasExactMatch = homeTeamMatch.HasExactMatch || awayTeamMatch.HasExactMatch,
                    HasPartialMatch = homeTeamMatch.HasPartialMatch || awayTeamMatch.HasPartialMatch
                };
            })
            .Where(gameMatch => gameMatch.HasPartialMatch)
            .ToList();

        var exactMatchedGameSummaries = gameMatches
            .Where(gameMatch => gameMatch.HasExactMatch)
            .Select(gameMatch => gameMatch.GameSummary)
            .DistinctBy(gameSummary => gameSummary.GameId)
            .ToList();
        if (exactMatchedGameSummaries.Count > 0) return exactMatchedGameSummaries;

        return [.. gameMatches.Select(gameMatch => gameMatch.GameSummary).DistinctBy(gameSummary => gameSummary.GameId)];
    }

    public static bool DoesTeamMatch(BaseballGameTeam team, string teamSearchText) =>
        GetTeamMatchResult(team, NormalizeTeamSearchText(teamSearchText)).HasPartialMatch;

    public static string GetMatchingTeamDisplayName(BaseballGameScheduleSummary gameSummary, string teamSearchText)
    {
        if (DoesTeamMatch(gameSummary.HomeParticipant.Team, teamSearchText)) return GetTeamDisplayName(gameSummary.HomeParticipant.Team);
        if (DoesTeamMatch(gameSummary.AwayParticipant.Team, teamSearchText)) return GetTeamDisplayName(gameSummary.AwayParticipant.Team);
        return teamSearchText;
    }

    public static IReadOnlyList<BaseballGameLiveEvent> GetLiveGameEvents(BaseballGameDetail gameDetail) =>
        gameDetail.LiveData?.LiveEvents
            .Where(liveEvent => !IsSeparatorEvent(liveEvent))
            .ToList() ?? [];

    public static string BuildLiveEventKey(BaseballGameLiveEvent liveEvent) =>
        string.Join("|",
            liveEvent.Period,
            liveEvent.BatterProviderPersonId,
            liveEvent.BallCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            liveEvent.Ball?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            liveEvent.Strike?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            liveEvent.Speed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            liveEvent.PitcherProviderPersonId,
            liveEvent.Text,
            liveEvent.PitchKind);

    public static bool HasCompleteLineups(BaseballGameDetail gameDetail) =>
        HasCompleteLineup(gameDetail.HomePlayers) && HasCompleteLineup(gameDetail.AwayPlayers);

    public static string FormatHomeAwayScoreLine(BaseballGameScheduleSummary gameSummary)
    {
        var homeTeamName = GetTeamDisplayName(gameSummary.HomeParticipant.Team);
        var awayTeamName = GetTeamDisplayName(gameSummary.AwayParticipant.Team);
        var homeScoreText = FormatParticipantScore(gameSummary.HomeParticipant, gameSummary.HomeScore);
        var awayScoreText = FormatParticipantScore(gameSummary.AwayParticipant, gameSummary.AwayScore);
        return $"홈 {homeTeamName} {homeScoreText} : {awayScoreText} {awayTeamName} 원정";
    }

    public static string FormatGameMatchDescription(BaseballGameScheduleSummary gameSummary) =>
        $"{GetTeamDisplayName(gameSummary.AwayParticipant.Team)} vs {GetTeamDisplayName(gameSummary.HomeParticipant.Team)}";

    public static string FormatGameStatus(BaseballGameScheduleSummary gameSummary, BaseballGameDetail? gameDetail)
    {
        if (IsRainCanceledGame(gameSummary)) return "우천취소";

        var gameStatusText = FormatPeriodText(gameSummary.GameStatus);
        if (IsCanceledStatusText(gameStatusText)) return gameStatusText;
        if (IsBeforeGame(gameSummary)) return "경기 전";
        if (IsEndedGame(gameSummary)) return "경기 종료";

        var periodCandidate = GetFirstText(gameDetail?.LiveData?.Ground?.LastPeriod, gameSummary.CurrentPeriod, gameSummary.PeriodType, gameSummary.GameStatus);
        var periodText = FormatPeriodText(periodCandidate);
        return string.IsNullOrWhiteSpace(periodText) ? "경기 중" : periodText;
    }

    public static bool IsBeforeGame(BaseballGameScheduleSummary gameSummary) =>
        gameSummary.GameStatus.Equals("BEFORE", StringComparison.OrdinalIgnoreCase) ||
        gameSummary.PeriodType.Equals("BEFORE", StringComparison.OrdinalIgnoreCase);

    public static bool IsEndedGame(BaseballGameScheduleSummary gameSummary) =>
        gameSummary.GameStatus.Equals("END", StringComparison.OrdinalIgnoreCase) ||
        gameSummary.PeriodType.Equals("END", StringComparison.OrdinalIgnoreCase);

    public static bool IsCanceledGame(BaseballGameScheduleSummary gameSummary)
    {
        var gameStatusText = FormatPeriodText(gameSummary.GameStatus);
        var periodTypeText = FormatPeriodText(gameSummary.PeriodType);
        return IsCanceledStatusText(gameStatusText) || IsCanceledStatusText(periodTypeText);
    }

    public static bool IsRainCanceledGame(BaseballGameScheduleSummary gameSummary) =>
        IsCanceledGame(gameSummary) &&
        gameSummary.GameDetailStatus.Equals("RAIN", StringComparison.OrdinalIgnoreCase);

    public static bool IsFinishedOrUnavailableGame(BaseballGameScheduleSummary gameSummary) =>
        IsEndedGame(gameSummary) || IsCanceledGame(gameSummary);

    public static string GetTeamDisplayName(BaseballGameTeam team)
    {
        if (!string.IsNullOrWhiteSpace(team.ShortName)) return team.ShortName;
        if (!string.IsNullOrWhiteSpace(team.Name)) return team.Name;
        return "팀 정보 없음";
    }

    private static void AppendCountAndBaseState(StringBuilder stringBuilder, BaseballGameDetail gameDetail)
    {
        var ground = gameDetail.LiveData?.Ground;
        if (ground == null)
        {
            stringBuilder.AppendLine("카운트: 정보 없음");
            stringBuilder.AppendLine("주자: 정보 없음");
            return;
        }

        var playerNameMap = BuildPlayerNameMap(gameDetail);
        stringBuilder.AppendLine($"카운트: 볼 {FormatNullableNumber(ground.Ball)} / 스트라이크 {FormatNullableNumber(ground.Strike)} / 아웃 {FormatNullableNumber(ground.Out)}");
        stringBuilder.AppendLine($"주자: 1루 {FormatRunner(ground.BaseFirstRunnerProviderPersonId, playerNameMap)} / 2루 {FormatRunner(ground.BaseSecondRunnerProviderPersonId, playerNameMap)} / 3루 {FormatRunner(ground.BaseThirdRunnerProviderPersonId, playerNameMap)}");
    }

    private static void AppendTeamScoreStatistics(StringBuilder stringBuilder, BaseballGameScheduleSummary gameSummary)
    {
        var homeTeamName = GetTeamDisplayName(gameSummary.HomeParticipant.Team);
        var awayTeamName = GetTeamDisplayName(gameSummary.AwayParticipant.Team);
        stringBuilder.AppendLine($"홈 {homeTeamName}: 안타 {FormatNullableNumber(gameSummary.HomeScore?.Hit)} / 실책 {FormatNullableNumber(gameSummary.HomeScore?.Error)} / 볼넷 {FormatNullableNumber(gameSummary.HomeScore?.Walks)}");
        stringBuilder.AppendLine($"원정 {awayTeamName}: 안타 {FormatNullableNumber(gameSummary.AwayScore?.Hit)} / 실책 {FormatNullableNumber(gameSummary.AwayScore?.Error)} / 볼넷 {FormatNullableNumber(gameSummary.AwayScore?.Walks)}");
    }

    private static void AppendBeforeGameInformation(StringBuilder stringBuilder, BaseballGameDetail gameDetail)
    {
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("라인업");

        var hasHomeLineup = AppendLineup(stringBuilder, "홈", gameDetail.GameSummary.HomeParticipant.Team, gameDetail.HomePlayers);
        var hasAwayLineup = AppendLineup(stringBuilder, "원정", gameDetail.GameSummary.AwayParticipant.Team, gameDetail.AwayPlayers);
        if (!hasHomeLineup && !hasAwayLineup) stringBuilder.AppendLine("라인업 미발표");

        var homeStartingPitcherName = FormatPlayerName(gameDetail.HomeStartingPitcher);
        var awayStartingPitcherName = FormatPlayerName(gameDetail.AwayStartingPitcher);
        stringBuilder.AppendLine($"선발투수: 홈 {GetTeamDisplayName(gameDetail.GameSummary.HomeParticipant.Team)} {homeStartingPitcherName} / 원정 {GetTeamDisplayName(gameDetail.GameSummary.AwayParticipant.Team)} {awayStartingPitcherName}");
    }

    private static bool AppendLineup(StringBuilder stringBuilder, string homeAwayText, BaseballGameTeam team, IReadOnlyList<BaseballGamePlayer> players)
    {
        var lineupPlayers = players
            .Where(player => player.BattingOrder.HasValue)
            .OrderBy(player => player.BattingOrder)
            .ToList();
        if (lineupPlayers.Count == 0) return false;

        stringBuilder.AppendLine($"{homeAwayText} {GetTeamDisplayName(team)}");
        foreach (var player in lineupPlayers) stringBuilder.AppendLine($"{player.BattingOrder}번 {FormatPlayerNameWithPosition(player)}");

        return true;
    }

    private static void AppendLiveGameInformation(StringBuilder stringBuilder, BaseballGameDetail gameDetail)
    {
        var recentEvents = GetLiveGameEvents(gameDetail)
            .TakeLast(5)
            .Reverse()
            .ToList();

        stringBuilder.AppendLine();
        stringBuilder.AppendLine("최근 이벤트");
        if (recentEvents.Count == 0)
        {
            stringBuilder.AppendLine("최근 이벤트가 없습니다.");
            return;
        }

        for (var eventIndex = 0; eventIndex < recentEvents.Count; eventIndex++)
        {
            var liveEvent = recentEvents[eventIndex];
            stringBuilder.Append($"{eventIndex + 1}. ");
            AppendLiveEventText(stringBuilder, gameDetail, liveEvent, includeDetails: eventIndex == 0);
        }
    }

    private static void AppendLiveEventText(
        StringBuilder stringBuilder,
        BaseballGameDetail gameDetail,
        BaseballGameLiveEvent liveEvent,
        bool includeDetails)
    {
        var periodText = FormatPeriodText(liveEvent.Period);
        var eventPeriodPrefix = string.IsNullOrWhiteSpace(periodText) ? string.Empty : $"[{periodText}] ";
        stringBuilder.AppendLine($"{eventPeriodPrefix}{liveEvent.Text}");
        if (includeDetails) AppendLatestEventDetails(stringBuilder, liveEvent, BuildPlayerNameMap(gameDetail));
    }

    private static void AppendLatestEventDetails(StringBuilder stringBuilder, BaseballGameLiveEvent liveEvent, IReadOnlyDictionary<string, string> playerNameMap)
    {
        var detailParts = new List<string>();
        var pitcherName = FindPlayerName(liveEvent.PitcherProviderPersonId, playerNameMap);
        var batterName = FindPlayerName(liveEvent.BatterProviderPersonId, playerNameMap);
        if (!string.IsNullOrWhiteSpace(pitcherName)) detailParts.Add($"투수: {pitcherName}");
        if (!string.IsNullOrWhiteSpace(batterName)) detailParts.Add($"타자: {batterName}");
        if (liveEvent.Speed is > 0) detailParts.Add($"구속: {liveEvent.Speed}km/h");
        if (!string.IsNullOrWhiteSpace(liveEvent.PitchKind)) detailParts.Add($"구종: {FormatPitchKind(liveEvent.PitchKind)}");
        if (liveEvent.BallCount is > 0) detailParts.Add($"카운트: B{FormatNullableNumber(liveEvent.Ball)}-S{FormatNullableNumber(liveEvent.Strike)}");
        if (detailParts.Count > 0) stringBuilder.AppendLine($"   {string.Join(" / ", detailParts)}");
    }

    private static void AppendAfterGameInformation(StringBuilder stringBuilder, BaseballGameDetail gameDetail)
    {
        var afterGameTexts = GetAfterGameTexts(gameDetail.LiveData?.LiveEvents ?? []);

        stringBuilder.AppendLine();
        stringBuilder.AppendLine("경기 후 정보");
        if (afterGameTexts.Count == 0)
        {
            stringBuilder.AppendLine("경기 후 정보가 없습니다.");
            return;
        }

        foreach (var afterGameText in afterGameTexts) stringBuilder.AppendLine(afterGameText);
    }

    private static void AppendCanceledGameInformation(StringBuilder stringBuilder, BaseballGameScheduleSummary gameSummary)
    {
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(IsRainCanceledGame(gameSummary)
            ? "우천취소로 경기가 취소되었습니다."
            : "경기가 취소되었습니다.");
    }

    private static IReadOnlyList<string> GetAfterGameTexts(IReadOnlyList<BaseballGameLiveEvent> liveEvents)
    {
        var separatorIndex = liveEvents
            .ToList()
            .FindIndex(IsSeparatorEvent);
        if (separatorIndex < 0) return [];

        return liveEvents
            .Skip(separatorIndex + 1)
            .Select(liveEvent => liveEvent.Text.Trim())
            .Where(liveEventText => !string.IsNullOrWhiteSpace(liveEventText))
            .ToList();
    }

    private static BaseballTeamMatchResult GetTeamMatchResult(BaseballGameTeam team, string normalizedTeamSearchText)
    {
        var normalizedSearchAliases = BuildTeamSearchAliases(team)
            .Select(NormalizeTeamSearchText)
            .Where(searchAlias => !string.IsNullOrWhiteSpace(searchAlias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasExactMatch = normalizedSearchAliases.Any(searchAlias =>
            searchAlias.Equals(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase));
        var hasPartialMatch = normalizedSearchAliases.Any(searchAlias =>
            searchAlias.Contains(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase) ||
            normalizedTeamSearchText.Contains(searchAlias, StringComparison.OrdinalIgnoreCase));

        return new BaseballTeamMatchResult(hasExactMatch, hasPartialMatch);
    }

    private static IReadOnlyList<string> BuildTeamSearchAliases(BaseballGameTeam team)
    {
        var searchAliases = new List<string>();
        if (!string.IsNullOrWhiteSpace(team.ShortName)) searchAliases.AddRange(BaseballTeamAliasCatalog.GetSearchAliases(team.ShortName));
        if (!string.IsNullOrWhiteSpace(team.Name)) searchAliases.Add(team.Name);
        if (!string.IsNullOrWhiteSpace(team.ShortName)) searchAliases.Add(team.ShortName);

        return searchAliases;
    }

    private static Dictionary<string, string> BuildPlayerNameMap(BaseballGameDetail gameDetail)
    {
        var playerNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in gameDetail.HomePlayers) AddPlayerName(playerNameMap, player);
        foreach (var player in gameDetail.AwayPlayers) AddPlayerName(playerNameMap, player);
        AddPlayerName(playerNameMap, gameDetail.HomeStartingPitcher);
        AddPlayerName(playerNameMap, gameDetail.AwayStartingPitcher);
        return playerNameMap;
    }

    private static void AddPlayerName(IDictionary<string, string> playerNameMap, BaseballGamePlayer? player)
    {
        if (player == null) return;
        if (string.IsNullOrWhiteSpace(player.ProviderPersonId) || string.IsNullOrWhiteSpace(player.Name)) return;
        playerNameMap.TryAdd(player.ProviderPersonId, player.Name);
    }

    private static string FormatParticipantScore(BaseballGameParticipant participant, BaseballGameScore? score)
    {
        if (score?.Run != null) return score.Run.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(participant.Result)) return participant.Result;
        return "-";
    }

    private static string FormatPeriodText(string? periodText)
    {
        if (string.IsNullOrWhiteSpace(periodText)) return string.Empty;
        if (periodText.Equals("BEFORE", StringComparison.OrdinalIgnoreCase)) return "경기 전";
        if (periodText.Equals("END", StringComparison.OrdinalIgnoreCase)) return "경기 종료";
        if (periodText.Equals("CANCEL", StringComparison.OrdinalIgnoreCase)) return "경기 취소";
        if (periodText.Equals("POSTPONE", StringComparison.OrdinalIgnoreCase)) return "경기 연기";
        if (periodText.Equals("PLAY", StringComparison.OrdinalIgnoreCase) ||
            periodText.Equals("LIVE", StringComparison.OrdinalIgnoreCase) ||
            periodText.Equals("START", StringComparison.OrdinalIgnoreCase))
            return "경기 중";
        if (periodText.Length >= 2 &&
            (periodText[0] == 'T' || periodText[0] == 'B') &&
            int.TryParse(periodText[1..], out var inning))
            return $"{inning}회{(periodText[0] == 'T' ? "초" : "말")}";

        return periodText;
    }

    private static string FormatStartTime(string startTime)
    {
        if (startTime.Length == 4) return $"{startTime[..2]}:{startTime[2..]}";
        return string.IsNullOrWhiteSpace(startTime) ? "시간 미정" : startTime;
    }

    private static string FormatFieldName(BaseballGameField? field)
    {
        if (field == null) return "정보 없음";
        if (!string.IsNullOrWhiteSpace(field.Name)) return field.Name;
        if (!string.IsNullOrWhiteSpace(field.ShortName)) return field.ShortName;
        return "정보 없음";
    }

    private static string FormatRunner(string providerPersonId, IReadOnlyDictionary<string, string> playerNameMap)
    {
        if (string.IsNullOrWhiteSpace(providerPersonId)) return "없음";
        var playerName = FindPlayerName(providerPersonId, playerNameMap);
        return string.IsNullOrWhiteSpace(playerName) ? providerPersonId : playerName;
    }

    private static string FindPlayerName(string providerPersonId, IReadOnlyDictionary<string, string> playerNameMap) =>
        !string.IsNullOrWhiteSpace(providerPersonId) && playerNameMap.TryGetValue(providerPersonId, out var playerName)
            ? playerName
            : string.Empty;

    private static string FormatPlayerName(BaseballGamePlayer? player) =>
        player == null || string.IsNullOrWhiteSpace(player.Name) ? "미정" : player.Name;

    private static string FormatPlayerNameWithPosition(BaseballGamePlayer player) =>
        string.IsNullOrWhiteSpace(player.PositionName) ? player.Name : $"{player.Name} ({player.PositionName})";

    private static string FormatNullableNumber(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    private static string FormatPitchKind(string pitchKind) =>
        pitchKind.ToUpperInvariant() switch
        {
            "FAST" => "직구",
            "TWOS" => "투심",
            "CUTT" => "커터",
            "SLID" => "슬라이더",
            "CURV" => "커브",
            "CHUP" => "체인지업",
            "FORK" => "포크",
            "SPLT" => "스플리터",
            "SINK" => "싱커",
            "SWEE" => "스위퍼",
            _ => pitchKind
        };

    private static string GetFirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool HasCompleteLineup(IReadOnlyList<BaseballGamePlayer> players) =>
        players
            .Where(player => player.BattingOrder is >= 1 and <= 9)
            .Select(player => player.BattingOrder!.Value)
            .Distinct()
            .Count() >= 9;

    private static bool IsCanceledStatusText(string statusText) =>
        statusText.Equals("경기 취소", StringComparison.Ordinal) ||
        statusText.Equals("경기 연기", StringComparison.Ordinal);

    private static bool IsSeparatorEvent(BaseballGameLiveEvent liveEvent) =>
        liveEvent.Text.Trim().StartsWith("====", StringComparison.Ordinal);

    private static string NormalizeTeamSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private sealed record BaseballTeamMatchResult(bool HasExactMatch, bool HasPartialMatch);
}
