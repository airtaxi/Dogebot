using Dogebot.Commons;
using Dogebot.Server.Baseball;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballGameSubscriptionCommandHandler(
    IBaseballGameScheduleService baseballGameScheduleService,
    IBaseballGameSubscriptionService baseballGameSubscriptionService,
    ILogger<BaseballGameSubscriptionCommandHandler> logger) : ICommandHandler
{
    private const string SubscribeCommand = "!야구구독";
    private const string UnsubscribeCommand = "!야구구독해제";

    public string Command => SubscribeCommand;

    public bool CanHandle(string content) => TryCreateCommandContext(content.Trim()) != null;

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var commandContext = TryCreateCommandContext(data.Content.Trim());
            if (commandContext == null)
                return CreateTextResponse(data.RoomId, "야구 구독 명령어를 처리할 수 없습니다.");

            if (string.IsNullOrWhiteSpace(commandContext.TeamSearchText))
                return CreateTextResponse(data.RoomId, $"사용법: {commandContext.Command} [팀명]\n예시: {commandContext.Command} KIA");

            if (commandContext.IsUnsubscribe)
                return await HandleUnsubscribeAsync(data, commandContext.TeamSearchText);

            return await HandleSubscribeAsync(data, commandContext.TeamSearchText);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_SUBSCRIPTION] Error processing baseball subscription command");
            return CreateTextResponse(data.RoomId, "야구 구독 처리 중 오류가 발생했습니다.");
        }
    }

    private async Task<ServerResponse> HandleSubscribeAsync(KakaoMessageData data, string teamSearchText)
    {
        var selectedGame = await FindSubscriptionGameAsync(teamSearchText);
        if (!selectedGame.Success)
            return CreateTextResponse(data.RoomId, selectedGame.Message);

        var gameDetail = await baseballGameScheduleService.GetGameDetailAsync(selectedGame.GameDate, selectedGame.GameSummary!.GameId);
        if (gameDetail == null)
        {
            return CreateTextResponse(
                data.RoomId,
                baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? "야구 경기 상세 정보를 가져오지 못했습니다.");
        }

        var subscribedTeamName = BaseballGameFormatter.GetMatchingTeamDisplayName(selectedGame.GameSummary, teamSearchText);
        var registerResult = await baseballGameSubscriptionService.RegisterAsync(
            data,
            selectedGame.GameDate,
            gameDetail,
            subscribedTeamName);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "[BASEBALL_SUBSCRIPTION] Baseball game subscription requested by {Sender} in room {RoomId} for {TeamSearchText}: {Result}",
                data.SenderName,
                data.RoomId,
                teamSearchText,
                registerResult.Message);

        return CreateTextResponse(data.RoomId, registerResult.Message);
    }

    private async Task<ServerResponse> HandleUnsubscribeAsync(KakaoMessageData data, string teamSearchText)
    {
        var removeResult = await baseballGameSubscriptionService.RemoveAsync(data.RoomId, teamSearchText);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "[BASEBALL_SUBSCRIPTION] Baseball game subscription removal requested by {Sender} in room {RoomId} for {TeamSearchText}: {Count}",
                data.SenderName,
                data.RoomId,
                teamSearchText,
                removeResult.RemovedCount);

        return CreateTextResponse(data.RoomId, removeResult.Message);
    }

    private async Task<BaseballGameSubscriptionSelectionResult> FindSubscriptionGameAsync(string teamSearchText)
    {
        var todaySnapshot = await baseballGameScheduleService.GetTodayGameSnapshotAsync();
        if (todaySnapshot == null)
        {
            return BaseballGameSubscriptionSelectionResult.CreateFailure(
                baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? "오늘 야구 경기 정보를 가져오지 못했습니다.");
        }

        var todayMatches = BaseballGameFormatter.FindMatchingGameSummaries(todaySnapshot.GameSummaries, teamSearchText);
        if (todayMatches.Count > 1)
            return CreateMultipleMatchResult(teamSearchText, todayMatches);

        var todayMatch = todayMatches.FirstOrDefault();
        if (todayMatch is not null && !BaseballGameFormatter.IsFinishedOrUnavailableGame(todayMatch))
            return BaseballGameSubscriptionSelectionResult.CreateSuccess(todaySnapshot.GameDate, todayMatch);

        var tomorrowSnapshot = await baseballGameScheduleService.GetTomorrowGameSnapshotAsync();
        if (tomorrowSnapshot == null)
        {
            return BaseballGameSubscriptionSelectionResult.CreateFailure(
                baseballGameScheduleService.GetLastGameScheduleErrorDetails() ?? "내일 야구 경기 정보를 가져오지 못했습니다.");
        }

        var tomorrowMatches = BaseballGameFormatter.FindMatchingGameSummaries(tomorrowSnapshot.GameSummaries, teamSearchText);
        if (tomorrowMatches.Count > 1)
            return CreateMultipleMatchResult(teamSearchText, tomorrowMatches);
        if (tomorrowMatches.Count == 1)
            return BaseballGameSubscriptionSelectionResult.CreateSuccess(tomorrowSnapshot.GameDate, tomorrowMatches[0]);

        if (todayMatch is not null)
            return BaseballGameSubscriptionSelectionResult.CreateFailure($"오늘 '{teamSearchText}' 팀 경기는 이미 종료됐거나 진행할 수 없는 상태이고 내일 해당 팀 경기가 없습니다.");

        return BaseballGameSubscriptionSelectionResult.CreateFailure($"오늘/내일 '{teamSearchText}' 팀 경기가 없습니다.");
    }

    private static BaseballGameSubscriptionSelectionResult CreateMultipleMatchResult(
        string teamSearchText,
        IReadOnlyList<BaseballGameScheduleSummary> gameSummaries)
    {
        var matchedGameDescriptions = string.Join(", ", gameSummaries.Select(BaseballGameFormatter.FormatGameMatchDescription));
        return BaseballGameSubscriptionSelectionResult.CreateFailure(
            $"'{teamSearchText}' 검색 결과가 여러 경기와 일치합니다: {matchedGameDescriptions}\n더 구체적으로 입력해주세요.");
    }

    private static BaseballGameSubscriptionCommandContext? TryCreateCommandContext(string content)
    {
        if (content.Equals(UnsubscribeCommand, StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith($"{UnsubscribeCommand} ", StringComparison.OrdinalIgnoreCase))
            return CreateCommandContext(content, UnsubscribeCommand, isUnsubscribe: true);

        if (content.Equals(SubscribeCommand, StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith($"{SubscribeCommand} ", StringComparison.OrdinalIgnoreCase))
            return CreateCommandContext(content, SubscribeCommand, isUnsubscribe: false);

        return null;
    }

    private static BaseballGameSubscriptionCommandContext CreateCommandContext(string content, string command, bool isUnsubscribe)
    {
        var teamSearchText = content.Length > command.Length ? content[command.Length..].Trim() : string.Empty;
        return new BaseballGameSubscriptionCommandContext(command, teamSearchText, isUnsubscribe);
    }

    private static ServerResponse CreateTextResponse(string roomId, string message) =>
        new()
        {
            Action = "send_text",
            RoomId = roomId,
            Message = message
        };

    private sealed record BaseballGameSubscriptionCommandContext(string Command, string TeamSearchText, bool IsUnsubscribe);

    private sealed record BaseballGameSubscriptionSelectionResult(
        bool Success,
        DateOnly GameDate,
        BaseballGameScheduleSummary? GameSummary,
        string Message)
    {
        public static BaseballGameSubscriptionSelectionResult CreateSuccess(DateOnly gameDate, BaseballGameScheduleSummary gameSummary) =>
            new(true, gameDate, gameSummary, string.Empty);

        public static BaseballGameSubscriptionSelectionResult CreateFailure(string message) =>
            new(false, default, null, message);
    }
}
