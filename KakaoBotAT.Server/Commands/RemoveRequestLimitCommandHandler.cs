using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RemoveRequestLimitCommandHandler : ICommandHandler
{
    private readonly IRequestLimitService _requestLimitService;
    private readonly IAdminService _adminService;
    private readonly ILogger<RemoveRequestLimitCommandHandler> _logger;

    public RemoveRequestLimitCommandHandler(
        IRequestLimitService requestLimitService,
        IAdminService adminService,
        ILogger<RemoveRequestLimitCommandHandler> logger)
    {
        _requestLimitService = requestLimitService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!제한해제";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 제한을 해제할 수 있습니다."
                };
            }

            var removed = await _requestLimitService.RemoveLimitAsync(data.RoomId, data.SenderHash);

            if (!removed)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[REQUEST_LIMIT_REMOVE] No limit found in room {RoomName} by {Sender}",
                        data.RoomName, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 이 방에는 설정된 요청 제한이 없습니다."
                };
            }

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[REQUEST_LIMIT_REMOVE] Limit removed from room {RoomName} by {Sender}",
                    data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 요청 제한 해제 완료!\n\n이제 이 방에서는 요청 횟수 제한이 없습니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REQUEST_LIMIT_REMOVE] Error processing remove request limit command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "요청 제한 해제 중 오류가 발생했습니다."
            };
        }
    }
}
