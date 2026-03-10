using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ScheduledMessageSetCommandHandler : ICommandHandler
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IAdminService _adminService;
    private readonly ILogger<ScheduledMessageSetCommandHandler> _logger;

    public ScheduledMessageSetCommandHandler(
        IScheduledMessageService scheduledMessageService,
        IAdminService adminService,
        ILogger<ScheduledMessageSetCommandHandler> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!반복설정";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 반복 메시지를 설정할 수 있습니다."
                };
            }

            _scheduledMessageService.StartSession(data.RoomId, data.SenderHash, data.SenderName, data.RoomName);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SCHEDULED_SET] Session started by {Sender} in room {RoomName}",
                    data.SenderName, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "📝 반복 메시지 설정\n\n" +
                         "보낼 메시지를 입력해주세요.\n" +
                         "줄바꿈도 포함됩니다.\n\n" +
                         "❌ 취소: !취소\n" +
                         "⏳ 5분 내에 입력하지 않으면 자동 취소됩니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SCHEDULED_SET] Error processing scheduled message set command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "반복 메시지 설정 중 오류가 발생했습니다."
            };
        }
    }
}
