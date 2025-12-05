using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class MyRankingCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<MyRankingCommandHandler> _logger;

    public MyRankingCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<MyRankingCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "/내랭킹";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var result = await _statisticsService.GetUserRankAsync(data.RoomId, data.SenderName);

            if (result == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{data.SenderName}님의 채팅 기록이 없습니다."
                };
            }

            var (rank, messageCount) = result.Value;
            var rankEmoji = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => "📊"
            };

            var message = $"{rankEmoji} {data.SenderName}님의 랭킹\n순위: {rank}위\n채팅 수: {messageCount:N0}회";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MY_RANKING] User {SenderName} is rank {Rank} with {Count} messages in room {RoomId}", 
                    data.SenderName, rank, messageCount, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MY_RANKING] Error processing my ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
