using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class MyMonthlyStatsCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<MyMonthlyStatsCommandHandler> _logger;

    public MyMonthlyStatsCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<MyMonthlyStatsCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!내월별통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var monthlyStats = await _statisticsService.GetUserMonthlyStatisticsAsync(data.RoomId, data.SenderHash);

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

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MY_MONTHLY_STATS] Showing personal monthly stats for {SenderName} in room {RoomId}",
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
            _logger.LogError(ex, "[MY_MONTHLY_STATS] Error processing personal monthly stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "월별통계 조회 중 오류가 발생했습니다."
            };
        }
    }
}
