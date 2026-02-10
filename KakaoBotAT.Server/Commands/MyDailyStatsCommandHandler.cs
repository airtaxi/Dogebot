using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class MyDailyStatsCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<MyDailyStatsCommandHandler> _logger;

    public MyDailyStatsCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<MyDailyStatsCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!내요일통계";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var dailyStats = await _statisticsService.GetUserDailyStatisticsAsync(data.RoomId, data.SenderHash);

            if (dailyStats.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{data.SenderName}님의 요일별 통계 데이터가 없습니다."
                };
            }

            var message = $"📅 {data.SenderName}님의 요일별 채팅 통계 (KST)\n\n" +
                          DailyStatsCommandHandler.FormatDailyStats(dailyStats);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MY_DAILY_STATS] Showing personal daily stats for {SenderName} in room {RoomId}",
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
            _logger.LogError(ex, "[MY_DAILY_STATS] Error processing personal daily stats command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "요일통계 조회 중 오류가 발생했습니다."
            };
        }
    }
}
