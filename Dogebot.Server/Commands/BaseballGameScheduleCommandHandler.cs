using System.Globalization;
using System.Text;
using Dogebot.Commons;
using Dogebot.Server.Baseball;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballGameScheduleCommandHandler(
    IBaseballGameScheduleService baseballGameScheduleService,
    ILogger<BaseballGameScheduleCommandHandler> logger) : ICommandHandler
{
    private const string TodayCommand = "!오늘야구";
    private const string TomorrowCommand = "!내일야구";

    public string Command => TodayCommand;

    public bool CanHandle(string content) => TryCreateCommandContext(content.Trim()) != null;

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var content = data.Content.Trim();
            var commandContext = TryCreateCommandContext(content);
            if (commandContext == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "야구 경기 명령어를 처리할 수 없습니다."
                };
            }

            var gameSnapshot = await GetGameSnapshotAsync(commandContext);
            if (gameSnapshot == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? $"{commandContext.DayLabel} 야구 경기 정보를 가져오지 못했습니다."
                };
            }

            if (string.IsNullOrWhiteSpace(commandContext.TeamSearchText))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = FormatGameSummaryMessage(gameSnapshot, commandContext.DayLabel)
                };
            }

            var matchedGameSummaries = FindMatchingGameSummaries(gameSnapshot.GameSummaries, commandContext.TeamSearchText);
            if (matchedGameSummaries.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{commandContext.DayLabel} '{commandContext.TeamSearchText}' 팀 경기가 없습니다.\n예시: {commandContext.Command} KIA, {commandContext.Command} LG, {commandContext.Command} 타이거즈"
                };
            }

            if (matchedGameSummaries.Count > 1)
            {
                var matchedGameDescriptions = string.Join(", ", matchedGameSummaries.Select(FormatGameMatchDescription));
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"'{commandContext.TeamSearchText}' 검색 결과가 여러 경기와 일치합니다: {matchedGameDescriptions}\n더 구체적으로 입력해주세요."
                };
            }

            var matchedGameSummary = matchedGameSummaries[0];
            var gameDetail = await GetGameDetailAsync(commandContext, matchedGameSummary.GameId);
            if (gameDetail == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? $"{commandContext.DayLabel} 야구 경기 상세 정보를 가져오지 못했습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_SCHEDULE] Baseball {DayLabel} game requested by {Sender} in room {RoomId} for {TeamSearchText}",
                    commandContext.DayLabel, data.SenderName, data.RoomId, commandContext.TeamSearchText);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatGameDetailMessage(gameDetail)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_SCHEDULE] Error processing baseball game schedule command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"Message: {exception.Message}\nStackTrace: {exception.StackTrace ?? "스택 추적 정보 없음"}"
            };
        }
    }

    private Task<BaseballGameScheduleSnapshot?> GetGameSnapshotAsync(BaseballGameCommandContext commandContext) =>
        commandContext.Command.Equals(TomorrowCommand, StringComparison.OrdinalIgnoreCase)
            ? baseballGameScheduleService.GetTomorrowGameSnapshotAsync()
            : baseballGameScheduleService.GetTodayGameSnapshotAsync();

    private Task<BaseballGameDetail?> GetGameDetailAsync(BaseballGameCommandContext commandContext, long gameId) =>
        commandContext.Command.Equals(TomorrowCommand, StringComparison.OrdinalIgnoreCase)
            ? baseballGameScheduleService.GetTomorrowGameDetailAsync(gameId)
            : baseballGameScheduleService.GetTodayGameDetailAsync(gameId);

    private static BaseballGameCommandContext? TryCreateCommandContext(string content)
    {
        if (content.StartsWith(TodayCommand, StringComparison.OrdinalIgnoreCase))
            return CreateCommandContext(content, TodayCommand, "오늘");
        if (content.StartsWith(TomorrowCommand, StringComparison.OrdinalIgnoreCase))
            return CreateCommandContext(content, TomorrowCommand, "내일");

        return null;
    }

    private static BaseballGameCommandContext CreateCommandContext(string content, string command, string dayLabel)
    {
        var teamSearchText = content.Length > command.Length ? content[command.Length..].Trim() : string.Empty;
        return new BaseballGameCommandContext(command, dayLabel, teamSearchText);
    }

    private static string FormatGameSummaryMessage(BaseballGameScheduleSnapshot gameSnapshot, string dayLabel)
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

    private static string FormatGameDetailMessage(BaseballGameDetail gameDetail)
    {
        var gameSummary = gameDetail.GameSummary;
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(FormatHomeAwayScoreLine(gameSummary));
        stringBuilder.AppendLine($"경기장: {FormatFieldName(gameSummary.Field)}");
        AppendCountAndBaseState(stringBuilder, gameDetail);
        AppendTeamScoreStatistics(stringBuilder, gameSummary);
        stringBuilder.AppendLine($"경기 상태: {FormatGameStatus(gameSummary, gameDetail)}");

        if (IsBeforeGame(gameSummary))
            AppendBeforeGameInformation(stringBuilder, gameDetail);
        else if (IsEndedGame(gameSummary))
            AppendAfterGameInformation(stringBuilder, gameDetail);
        else
            AppendLiveGameInformation(stringBuilder, gameDetail);

        return stringBuilder.ToString().TrimEnd();
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
        foreach (var player in lineupPlayers)
            stringBuilder.AppendLine($"{player.BattingOrder}번 {FormatPlayerNameWithPosition(player)}");

        return true;
    }

    private static void AppendLiveGameInformation(StringBuilder stringBuilder, BaseballGameDetail gameDetail)
    {
        var recentEvents = gameDetail.LiveData?.LiveEvents
            .Where(liveEvent => !IsSeparatorEvent(liveEvent))
            .TakeLast(5)
            .Reverse()
            .ToList() ?? [];

        stringBuilder.AppendLine();
        stringBuilder.AppendLine("최근 이벤트");
        if (recentEvents.Count == 0)
        {
            stringBuilder.AppendLine("최근 이벤트가 없습니다.");
            return;
        }

        var playerNameMap = BuildPlayerNameMap(gameDetail);
        for (var eventIndex = 0; eventIndex < recentEvents.Count; eventIndex++)
        {
            var liveEvent = recentEvents[eventIndex];
            var periodText = FormatPeriodText(liveEvent.Period);
            var eventPeriodPrefix = string.IsNullOrWhiteSpace(periodText) ? string.Empty : $"[{periodText}] ";
            stringBuilder.AppendLine($"{eventIndex + 1}. {eventPeriodPrefix}{liveEvent.Text}");
            if (eventIndex == 0) AppendLatestEventDetails(stringBuilder, liveEvent, playerNameMap);
        }
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

        foreach (var afterGameText in afterGameTexts)
            stringBuilder.AppendLine(afterGameText);
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

    private static List<BaseballGameScheduleSummary> FindMatchingGameSummaries(IReadOnlyList<BaseballGameScheduleSummary> gameSummaries, string teamSearchText)
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

    private static string FormatHomeAwayScoreLine(BaseballGameScheduleSummary gameSummary)
    {
        var homeTeamName = GetTeamDisplayName(gameSummary.HomeParticipant.Team);
        var awayTeamName = GetTeamDisplayName(gameSummary.AwayParticipant.Team);
        var homeScoreText = FormatParticipantScore(gameSummary.HomeParticipant, gameSummary.HomeScore);
        var awayScoreText = FormatParticipantScore(gameSummary.AwayParticipant, gameSummary.AwayScore);
        return $"홈 {homeTeamName} {homeScoreText} : {awayScoreText} {awayTeamName} 원정";
    }

    private static string FormatGameMatchDescription(BaseballGameScheduleSummary gameSummary) =>
        $"{GetTeamDisplayName(gameSummary.AwayParticipant.Team)} vs {GetTeamDisplayName(gameSummary.HomeParticipant.Team)}";

    private static string FormatParticipantScore(BaseballGameParticipant participant, BaseballGameScore? score)
    {
        if (score?.Run != null) return score.Run.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(participant.Result)) return participant.Result;
        return "-";
    }

    private static string FormatGameStatus(BaseballGameScheduleSummary gameSummary, BaseballGameDetail? gameDetail)
    {
        var gameStatusText = FormatPeriodText(gameSummary.GameStatus);
        if (IsCanceledStatusText(gameStatusText)) return gameStatusText;
        if (IsBeforeGame(gameSummary)) return "경기 전";
        if (IsEndedGame(gameSummary)) return "경기 종료";

        var periodCandidate = GetFirstText(gameDetail?.LiveData?.Ground?.LastPeriod, gameSummary.CurrentPeriod, gameSummary.PeriodType, gameSummary.GameStatus);
        var periodText = FormatPeriodText(periodCandidate);
        return string.IsNullOrWhiteSpace(periodText) ? "경기 중" : periodText;
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

    private static string GetTeamDisplayName(BaseballGameTeam team)
    {
        if (!string.IsNullOrWhiteSpace(team.ShortName)) return team.ShortName;
        if (!string.IsNullOrWhiteSpace(team.Name)) return team.Name;
        return "팀 정보 없음";
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

    private static bool IsBeforeGame(BaseballGameScheduleSummary gameSummary) =>
        gameSummary.GameStatus.Equals("BEFORE", StringComparison.OrdinalIgnoreCase) ||
        gameSummary.PeriodType.Equals("BEFORE", StringComparison.OrdinalIgnoreCase);

    private static bool IsEndedGame(BaseballGameScheduleSummary gameSummary) =>
        gameSummary.GameStatus.Equals("END", StringComparison.OrdinalIgnoreCase) ||
        gameSummary.PeriodType.Equals("END", StringComparison.OrdinalIgnoreCase);

    private static bool IsCanceledStatusText(string statusText) =>
        statusText.Equals("경기 취소", StringComparison.Ordinal) ||
        statusText.Equals("경기 연기", StringComparison.Ordinal);

    private static bool IsSeparatorEvent(BaseballGameLiveEvent liveEvent) =>
        liveEvent.Text.Trim().StartsWith("====", StringComparison.Ordinal);

    private static string NormalizeTeamSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private sealed record BaseballTeamMatchResult(bool HasExactMatch, bool HasPartialMatch);

    private sealed record BaseballGameCommandContext(string Command, string DayLabel, string TeamSearchText);
}
