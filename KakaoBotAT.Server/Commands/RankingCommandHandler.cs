using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RankingCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<RankingCommandHandler> _logger;

    public RankingCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<RankingCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "/랭킹";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var topUsers = await _statisticsService.GetTopUsersAsync(data.RoomId, 10);

            if (topUsers.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 통계 데이터가 없습니다."
                };
            }

            var message = "📊 채팅 랭킹 TOP 10\n\n";
            for (int i = 0; i < topUsers.Count; i++)
            {
                var (senderName, messageCount) = topUsers[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{i + 1}."
                };
                message += $"{medal} {senderName}: {messageCount:N0}회\n";
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RANKING] Showing rankings for room {RoomId}", data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RANKING] Error processing ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
