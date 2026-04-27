using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class MyMonthlyStatsCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<MyMonthlyStatsCommandHandler> logger) : ICommandHandler
{
    public string Command => "!내월별통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var monthlyStats = await statisticsService.GetUserMonthlyStatisticsAsync(data.RoomId, data.SenderHash);

            if (monthlyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{data.SenderName}님의 월별 통계 데이터가 없습니다."
                };
            }

            var message = $"📆 {data.SenderName}님의 월별 채팅 통계 (KST)\n\n" +
                          MonthlyStatsCommandHandler.FormatMonthlyStats(monthlyStats);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[MY_MONTHLY_STATS] Showing personal monthly stats for {SenderName} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MY_MONTHLY_STATS] Error processing personal monthly stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "월별통계 조회 중 오류가 발생했습니다."
            };
        }
    }
}

