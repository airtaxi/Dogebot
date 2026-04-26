using System.Globalization;
using System.Text;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class BaseballCrowdRankingCommandHandler(
    IBaseballTeamRankingService baseballTeamRankingService,
    ILogger<BaseballCrowdRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구관중순위";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var baseballCrowdRankingSnapshot = await baseballTeamRankingService.GetBaseballCrowdRankingSnapshotAsync();
            if (baseballCrowdRankingSnapshot == null || baseballCrowdRankingSnapshot.CrowdRankings.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballTeamRankingService.GetLastCrowdRankingErrorDetails() ?? "야구 관중 순위를 가져오지 못했습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_CROWD_RANKING] Baseball crowd ranking requested by {Sender} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatCrowdRankingMessage(baseballCrowdRankingSnapshot)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_CROWD_RANKING] Error processing baseball crowd ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"Message: {exception.Message}\nStackTrace: {exception.StackTrace ?? "스택 추적 정보 없음"}"
            };
        }
    }

    private static string FormatCrowdRankingMessage(BaseballCrowdRankingSnapshot baseballCrowdRankingSnapshot)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ KBO 관중 순위 ({baseballCrowdRankingSnapshot.DateText})");
        stringBuilder.AppendLine();

        foreach (var crowdRanking in baseballCrowdRankingSnapshot.CrowdRankings)
            stringBuilder.AppendLine($"{crowdRanking.Rank}위 {crowdRanking.TeamName} - {crowdRanking.CrowdCount.ToString("N0", CultureInfo.InvariantCulture)}명");

        return stringBuilder.ToString().TrimEnd();
    }
}
