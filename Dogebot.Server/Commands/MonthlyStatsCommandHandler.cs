using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class MonthlyStatsCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<MonthlyStatsCommandHandler> logger) : ICommandHandler
{
    private static readonly string[] MonthNames = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"];

    public string Command => "!월별통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var monthlyStats = await statisticsService.GetMonthlyStatisticsAsync(data.RoomId);

            if (monthlyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 월별 통계 데이터가 없습니다."
                };
            }

            var message = "📆 월별 채팅 통계 (KST)\n\n" + FormatMonthlyStats(monthlyStats);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[MONTHLY_STATS] Showing room monthly stats for room {RoomId}", data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MONTHLY_STATS] Error processing monthly stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "월별통계 조회 중 오류가 발생했습니다."
            };
        }
    }

    /// <summary>
    /// Formats monthly statistics as a visual bar chart for chat display.
    /// </summary>
    internal static string FormatMonthlyStats(List<(int Month, long MessageCount)> monthlyStats)
    {
        var lookup = monthlyStats.ToDictionary(x => x.Month, x => x.MessageCount);
        var maxCount = monthlyStats.Max(x => x.MessageCount);
        const int maxBarLength = 8;

        var lines = new List<string>();
        for (var month = 1; month <= 12; month++)
        {
            var count = lookup.GetValueOrDefault(month, 0);
            var barLength = maxCount > 0 ? (int)((double)count / maxCount * maxBarLength) : 0;
            var bar = new string('█', barLength);
            lines.Add($"{MonthNames[month - 1],2}월 {bar} {count:N0}");
        }

        var peakMonth = monthlyStats.MaxBy(x => x.MessageCount);
        lines.Add($"\n🔥 최고 활동 월: {peakMonth.Month}월 ({peakMonth.MessageCount:N0}회)");

        return string.Join('\n', lines);
    }
}

