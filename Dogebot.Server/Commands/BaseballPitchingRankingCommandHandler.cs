using System.Text;
using Dogebot.Commons;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballPitchingRankingCommandHandler(
    IBaseballTeamRankingService baseballTeamRankingService,
    ILogger<BaseballPitchingRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구투수순위";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var baseballTopFiveSnapshot = await baseballTeamRankingService.GetBaseballTopFiveSnapshotAsync();
            if (baseballTopFiveSnapshot == null || baseballTopFiveSnapshot.PitchingTopFiveStatistics.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballTeamRankingService.GetLastPlayerTopFiveErrorDetails() ?? "야구 투수 순위를 가져오지 못했습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_PITCHING_RANKING] Baseball pitching ranking requested by {Sender} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatPitchingRankingMessage(baseballTopFiveSnapshot.PitchingTopFiveStatistics)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_PITCHING_RANKING] Error processing baseball pitching ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"Message: {exception.Message}\nStackTrace: {exception.StackTrace ?? "스택 추적 정보 없음"}"
            };
        }
    }

    private static string FormatPitchingRankingMessage(IReadOnlyList<BaseballTopFiveStatistic> pitchingTopFiveStatistics)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("⚾ KBO 투수 TOP5");
        stringBuilder.AppendLine();

        for (var statisticIndex = 0; statisticIndex < pitchingTopFiveStatistics.Count; statisticIndex++)
        {
            var pitchingTopFiveStatistic = pitchingTopFiveStatistics[statisticIndex];
            stringBuilder.AppendLine($"{pitchingTopFiveStatistic.StatisticName} TOP5");

            foreach (var playerEntry in pitchingTopFiveStatistic.PlayerEntries.OrderBy(playerEntry => playerEntry.Rank))
                stringBuilder.AppendLine($"{playerEntry.Rank}위 {playerEntry.PlayerName} ({playerEntry.TeamName}) - {playerEntry.StatisticValue}");

            if (statisticIndex < pitchingTopFiveStatistics.Count - 1) stringBuilder.AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}

