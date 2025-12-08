using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class EnableRankingCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _chatStatisticsService;
    private readonly IAdminService _adminService;
    private readonly ILogger<EnableRankingCommandHandler> _logger;

    public EnableRankingCommandHandler(
        IChatStatisticsService chatStatisticsService,
        IAdminService adminService,
        ILogger<EnableRankingCommandHandler> logger)
    {
        _chatStatisticsService = chatStatisticsService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!랭크활성화";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 랭킹을 활성화할 수 있습니다."
                };
            }

            var isEnabled = await _chatStatisticsService.IsMessageContentEnabledAsync(data.RoomId);

            if (isEnabled)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이미 랭킹이 활성화되어 있습니다."
                };
            }

            await _chatStatisticsService.EnableMessageContentAsync(data.RoomId, data.RoomName, data.SenderHash);

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RANKING_ENABLE] Ranking enabled for room {RoomName} by {Sender}",
                    data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 랭킹 활성화 완료!\n\n" +
                         "이제 이 방에서 메시지 내용이 기록되며\n" +
                         "!랭크 명령어를 사용할 수 있습니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RANKING_ENABLE] Error processing enable ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 활성화 중 오류가 발생했습니다."
            };
        }
    }
}
