using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class DisableRankingCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _chatStatisticsService;
    private readonly IAdminService _adminService;
    private readonly ILogger<DisableRankingCommandHandler> _logger;

    public DisableRankingCommandHandler(
        IChatStatisticsService chatStatisticsService,
        IAdminService adminService,
        ILogger<DisableRankingCommandHandler> logger)
    {
        _chatStatisticsService = chatStatisticsService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!랭크비활성화";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await _adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 관리자만 랭킹을 비활성화할 수 있습니다."
                };
            }

            var isEnabled = await _chatStatisticsService.IsMessageContentEnabledAsync(data.RoomId);

            if (!isEnabled)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이미 랭킹이 비활성화되어 있습니다."
                };
            }

            await _chatStatisticsService.DisableMessageContentAsync(data.RoomId, data.RoomName, data.SenderHash);

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RANKING_DISABLE] Ranking disabled and message content deleted for room {RoomName} by {Sender}",
                    data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 랭킹 비활성화 완료!\n\n" +
                         "기존 메시지 내용 기록이 모두 삭제되었습니다.\n" +
                         "이제 메시지 내용이 기록되지 않으며\n" +
                         "!랭크 명령어를 사용할 수 없습니다.\n\n" +
                         "💡 채팅 통계(!조회, !내랭킹 등)는 계속 사용 가능합니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RANKING_DISABLE] Error processing disable ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 비활성화 중 오류가 발생했습니다."
            };
        }
    }
}
