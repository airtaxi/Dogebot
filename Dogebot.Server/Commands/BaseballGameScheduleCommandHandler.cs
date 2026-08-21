using Dogebot.Commons;
using Dogebot.Server.Baseball;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballGameScheduleCommandHandler(IBaseballGameScheduleService baseballGameScheduleService, IUserBaseballTeamPreferenceService userBaseballTeamPreferenceService, ILogger<BaseballGameScheduleCommandHandler> logger) : ICommandHandler
{
    private const string TodayCommand = "!오늘야구";
    private const string TodayAliasCommand = "!야구";
    private const string TomorrowCommand = "!내일야구";
    private const string YesterdayCommand = "!어제야구";

    public string Command => TodayCommand;

    public bool CanHandle(string content) => TryCreateCommandContext(content.Trim()) != null;

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var commandContext = TryCreateCommandContext(data.Content.Trim());
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

            var teamSearchText = commandContext.TeamSearchText;
            if (string.IsNullOrWhiteSpace(teamSearchText)) teamSearchText = await userBaseballTeamPreferenceService.GetUserPreferredTeamAsync(data.SenderHash) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(teamSearchText))
            {
                var gameDetailsByGameId = await GetGameDetailsByGameIdAsync(commandContext, gameSnapshot.GameSummaries);
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = BaseballGameFormatter.FormatGameSummaryMessage(gameSnapshot, commandContext.DayLabel, gameDetailsByGameId)
                };
            }

            var matchedGameSummaries = BaseballGameFormatter.FindMatchingGameSummaries(gameSnapshot.GameSummaries, teamSearchText);
            if (matchedGameSummaries.Count == 0) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"{commandContext.DayLabel} '{teamSearchText}' 팀 경기가 없습니다.\n예시: {commandContext.Command} KIA, {commandContext.Command} LG, {commandContext.Command} 타이거즈"};

            if (matchedGameSummaries.Count > 1)
            {
                var matchedGameDescriptions = string.Join(", ", matchedGameSummaries.Select(BaseballGameFormatter.FormatGameMatchDescription));
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"'{teamSearchText}' 검색 결과가 여러 경기와 일치합니다: {matchedGameDescriptions}\n더 구체적으로 입력해주세요."
                };
            }

            var matchedGameSummary = matchedGameSummaries[0];
            var gameDetail = await GetGameDetailAsync(commandContext, matchedGameSummary.GameId);
            if (gameDetail == null) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? $"{commandContext.DayLabel} 야구 경기 상세 정보를 가져오지 못했습니다."};

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[BASEBALL_SCHEDULE] Baseball {DayLabel} game requested by {Sender} in room {RoomId} for {TeamSearchText}", commandContext.DayLabel, data.SenderName, data.RoomId, teamSearchText);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = BaseballGameFormatter.FormatGameDetailMessage(gameDetail)
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
        commandContext.Command.Equals(TomorrowCommand, StringComparison.OrdinalIgnoreCase) ? baseballGameScheduleService.GetTomorrowGameSnapshotAsync() :
        commandContext.Command.Equals(YesterdayCommand, StringComparison.OrdinalIgnoreCase) ? baseballGameScheduleService.GetYesterdayGameSnapshotAsync() :
        baseballGameScheduleService.GetTodayGameSnapshotAsync();

    private Task<BaseballGameDetail?> GetGameDetailAsync(BaseballGameCommandContext commandContext, long gameId) =>
        commandContext.Command.Equals(TomorrowCommand, StringComparison.OrdinalIgnoreCase) ? baseballGameScheduleService.GetTomorrowGameDetailAsync(gameId) :
        commandContext.Command.Equals(YesterdayCommand, StringComparison.OrdinalIgnoreCase) ? baseballGameScheduleService.GetYesterdayGameDetailAsync(gameId) :
        baseballGameScheduleService.GetTodayGameDetailAsync(gameId);

    private async Task<IReadOnlyDictionary<long, BaseballGameDetail>> GetGameDetailsByGameIdAsync(BaseballGameCommandContext commandContext, IReadOnlyList<BaseballGameScheduleSummary> gameSummaries)
    {
        var gameSummariesRequiringDetails = gameSummaries
            .Where(ShouldFetchGameDetailForLeftOnBase)
            .ToList();
        if (gameSummariesRequiringDetails.Count == 0) return new Dictionary<long, BaseballGameDetail>();

        var gameDetailTasks = gameSummariesRequiringDetails
            .Select(async gameSummary =>
            {
                var gameDetail = await GetGameDetailAsync(commandContext, gameSummary.GameId);
                return new BaseballGameDetailResult(gameSummary.GameId, gameDetail);
            });
        var gameDetailResults = await Task.WhenAll(gameDetailTasks);
        return gameDetailResults
            .Where(gameDetailResult => gameDetailResult.GameDetail != null)
            .ToDictionary(gameDetailResult => gameDetailResult.GameId, gameDetailResult => gameDetailResult.GameDetail!);
    }

    private static bool ShouldFetchGameDetailForLeftOnBase(BaseballGameScheduleSummary gameSummary)
    {
        var hasLeftOnBase = gameSummary.HomeTeamStatistics?.BattingLeftOnBase != null || gameSummary.AwayTeamStatistics?.BattingLeftOnBase != null;
        if (hasLeftOnBase) return false;
        return !BaseballGameFormatter.IsBeforeGame(gameSummary) && !BaseballGameFormatter.IsCanceledGame(gameSummary);
    }

    private static BaseballGameCommandContext? TryCreateCommandContext(string content)
    {
        if (content.StartsWith(TodayCommand, StringComparison.OrdinalIgnoreCase))
            return CreateCommandContext(content, TodayCommand, "오늘");
        if (content.Equals(TodayAliasCommand, StringComparison.OrdinalIgnoreCase) || content.StartsWith($"{TodayAliasCommand} ", StringComparison.OrdinalIgnoreCase)) return CreateCommandContext(content, TodayAliasCommand, "오늘");
        if (content.StartsWith(TomorrowCommand, StringComparison.OrdinalIgnoreCase)) return CreateCommandContext(content, TomorrowCommand, "내일");
        if (content.StartsWith(YesterdayCommand, StringComparison.OrdinalIgnoreCase)) return CreateCommandContext(content, YesterdayCommand, "어제");

        return null;
    }

    private static BaseballGameCommandContext CreateCommandContext(string content, string command, string dayLabel)
    {
        var teamSearchText = content.Length > command.Length ? content[command.Length..].Trim() : string.Empty;
        return new BaseballGameCommandContext(command, dayLabel, teamSearchText);
    }

    private sealed record BaseballGameCommandContext(string Command, string DayLabel, string TeamSearchText);

    private sealed record BaseballGameDetailResult(long GameId, BaseballGameDetail? GameDetail);
}
