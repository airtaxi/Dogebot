using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RankingCommandHandler : ICommandHandler
{
    private readonly ILogger<RankingCommandHandler> _logger;

    public RankingCommandHandler(ILogger<RankingCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!랭킹";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var message = "📊 채팅 랭킹 조회 안내\n\n" +
                         "키워드 알림 방지를 위해 개인톡에서 사용하는 것을 권장합니다.\n\n" +
                         "사용법: !조회 (roomId)\n" +
                         $"현재 방 조회: !조회 {data.RoomId}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RANKING] Showing ranking guide for room {RoomId}", data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RANKING] Error processing ranking command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 안내 중 오류가 발생했습니다."
            });
        }
    }
}
