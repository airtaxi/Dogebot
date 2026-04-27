using System.Text;
using Dogebot.Commons;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballBattingRankingCommandHandler(
    IBaseballTeamRankingService baseballTeamRankingService,
    ILogger<BaseballBattingRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구타자순위";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var baseballTopFiveSnapshot = await baseballTeamRankingService.GetBaseballTopFiveSnapshotAsync();
            if (baseballTopFiveSnapshot == null || baseballTopFiveSnapshot.BattingTopFiveStatistics.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballTeamRankingService.GetLastPlayerTopFiveErrorDetails() ?? "야구 타자 순위를 가져오지 못했습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_BATTING_RANKING] Baseball batting ranking requested by {Sender} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatBattingRankingMessage(baseballTopFiveSnapshot.BattingTopFiveStatistics)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_BATTING_RANKING] Error processing baseball batting ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"Message: {exception.Message}\nStackTrace: {exception.StackTrace ?? "스택 추적 정보 없음"}"
            };
        }
    }

    private static string FormatBattingRankingMessage(IReadOnlyList<BaseballTopFiveStatistic> battingTopFiveStatistics)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("⚾ KBO 타자 TOP5");
        stringBuilder.AppendLine();

        for (var statisticIndex = 0; statisticIndex < battingTopFiveStatistics.Count; statisticIndex++)
        {
            var battingTopFiveStatistic = battingTopFiveStatistics[statisticIndex];
            stringBuilder.AppendLine($"{battingTopFiveStatistic.StatisticName} TOP5");

            foreach (var playerEntry in battingTopFiveStatistic.PlayerEntries.OrderBy(playerEntry => playerEntry.Rank))
                stringBuilder.AppendLine($"{playerEntry.Rank}위 {playerEntry.PlayerName} ({playerEntry.TeamName}) - {playerEntry.StatisticValue}");

            if (statisticIndex < battingTopFiveStatistics.Count - 1) stringBuilder.AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}

