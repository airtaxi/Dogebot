using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class DailyStatsCommandHandler : ICommandHandler
{
    private static readonly string[] DayNames = ["일", "월", "화", "수", "목", "금", "토"];

    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<DailyStatsCommandHandler> _logger;

    public DailyStatsCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<DailyStatsCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!요일통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var dailyStats = await _statisticsService.GetDailyStatisticsAsync(data.RoomId);

            if (dailyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 요일별 통계 데이터가 없습니다."
                };
            }

            var message = "📅 요일별 채팅 통계 (KST)\n\n" + FormatDailyStats(dailyStats);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[DAILY_STATS] Showing room daily stats for room {RoomId}", data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DAILY_STATS] Error processing daily stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "요일통계 조회 중 오류가 발생했습니다."
            };
        }
    }

    /// <summary>
    /// Formats daily statistics as a visual bar chart for chat display.
    /// </summary>
    internal static string FormatDailyStats(List<(DayOfWeek Day, long MessageCount)> dailyStats)
    {
        var lookup = dailyStats.ToDictionary(x => x.Day, x => x.MessageCount);
        var maxCount = dailyStats.Max(x => x.MessageCount);
        const int maxBarLength = 8;

        var lines = new List<string>();
        // Sunday(0) through Saturday(6)
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)i;
            var count = lookup.GetValueOrDefault(day, 0);
            var barLength = maxCount > 0 ? (int)((double)count / maxCount * maxBarLength) : 0;
            var bar = new string('█', barLength);
            lines.Add($"{DayNames[i]}요일 {bar} {count:N0}");
        }

        var peakDay = dailyStats.MaxBy(x => x.MessageCount);
        lines.Add($"\n🔥 최고 활동 요일: {DayNames[(int)peakDay.Day]}요일 ({peakDay.MessageCount:N0}회)");

        return string.Join('\n', lines);
    }
}
