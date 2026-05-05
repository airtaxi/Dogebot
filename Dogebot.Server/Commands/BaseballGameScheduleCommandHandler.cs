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

            if (string.IsNullOrWhiteSpace(commandContext.TeamSearchText))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = BaseballGameFormatter.FormatGameSummaryMessage(gameSnapshot, commandContext.DayLabel)
                };
            }

            var matchedGameSummaries = BaseballGameFormatter.FindMatchingGameSummaries(gameSnapshot.GameSummaries, commandContext.TeamSearchText);
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
                var matchedGameDescriptions = string.Join(", ", matchedGameSummaries.Select(BaseballGameFormatter.FormatGameMatchDescription));
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

    private sealed record BaseballGameCommandContext(string Command, string DayLabel, string TeamSearchText);
}
