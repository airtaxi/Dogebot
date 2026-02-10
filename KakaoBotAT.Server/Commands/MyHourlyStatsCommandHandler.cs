using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class MyHourlyStatsCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<MyHourlyStatsCommandHandler> _logger;

    public MyHourlyStatsCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<MyHourlyStatsCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!내시간통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var hourlyStats = await _statisticsService.GetUserHourlyStatisticsAsync(data.RoomId, data.SenderHash);

            if (hourlyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{data.SenderName}님의 시간별 통계 데이터가 없습니다."
                };
            }

            var message = $"⏰ {data.SenderName}님의 시간대별 채팅 통계 (KST)\n\n" +
                          HourlyStatsCommandHandler.FormatHourlyStats(hourlyStats);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MY_HOURLY_STATS] Showing personal hourly stats for {SenderName} in room {RoomId}",
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
            _logger.LogError(ex, "[MY_HOURLY_STATS] Error processing personal hourly stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "시간통계 조회 중 오류가 발생했습니다."
            };
        }
    }
}
