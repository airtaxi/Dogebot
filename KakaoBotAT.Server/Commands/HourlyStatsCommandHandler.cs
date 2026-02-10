using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class HourlyStatsCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<HourlyStatsCommandHandler> _logger;

    public HourlyStatsCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<HourlyStatsCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!시간통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var hourlyStats = await _statisticsService.GetHourlyStatisticsAsync(data.RoomId);

            if (hourlyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 시간별 통계 데이터가 없습니다."
                };
            }

            var message = "⏰ 시간대별 채팅 통계 (KST)\n\n" + FormatHourlyStats(hourlyStats);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[HOURLY_STATS] Showing room hourly stats for room {RoomId}", data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HOURLY_STATS] Error processing hourly stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "시간통계 조회 중 오류가 발생했습니다."
            };
        }
    }

    /// <summary>
    /// Formats hourly statistics as a visual bar chart for chat display.
    /// </summary>
    internal static string FormatHourlyStats(List<(int Hour, long MessageCount)> hourlyStats)
    {
        var lookup = hourlyStats.ToDictionary(x => x.Hour, x => x.MessageCount);
        var maxCount = hourlyStats.Max(x => x.MessageCount);
        const int maxBarLength = 8;

        var lines = new List<string>();
        for (var hour = 0; hour < 24; hour++)
        {
            var count = lookup.GetValueOrDefault(hour, 0);
            var barLength = maxCount > 0 ? (int)((double)count / maxCount * maxBarLength) : 0;
            var bar = new string('█', barLength);
            lines.Add($"{hour,2}시 {bar} {count:N0}");
        }

        var peakHour = hourlyStats.MaxBy(x => x.MessageCount);
        lines.Add($"\n🔥 최고 활동 시간: {peakHour.Hour}시 ({peakHour.MessageCount:N0}회)");

        return string.Join('\n', lines);
    }
}
