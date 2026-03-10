using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ScheduledMessageListCommandHandler : ICommandHandler
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IAdminService _adminService;
    private readonly ILogger<ScheduledMessageListCommandHandler> _logger;

    public ScheduledMessageListCommandHandler(
        IScheduledMessageService scheduledMessageService,
        IAdminService adminService,
        ILogger<ScheduledMessageListCommandHandler> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!반복목록";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 반복 메시지 목록을 조회할 수 있습니다."
                };
            }

            var messages = await _scheduledMessageService.GetScheduledMessagesAsync(data.RoomId);

            if (messages.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이 방에 설정된 반복 메시지가 없습니다.\n\n" +
                             "!반복설정으로 새 반복 메시지를 추가할 수 있습니다."
                };
            }

            var result = "📋 반복 메시지 목록\n\n";
            for (int i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                var preview = message.Message.Length > 30
                    ? message.Message[..27] + "..."
                    : message.Message;
                preview = preview.Replace("\n", " ").Replace("\r", "");
                var hoursDisplay = string.Join(", ", message.Hours.Select(h => $"{h}시"));
                result += $"{i + 1}. \"{preview}\"\n   ⏰ {hoursDisplay} | 👤 {message.CreatedByName}\n\n";
            }

            result = result.TrimEnd();

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SCHEDULED_LIST] Showing {Count} scheduled messages for room {RoomName}",
                    messages.Count, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SCHEDULED_LIST] Error processing scheduled message list command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "반복 메시지 목록 조회 중 오류가 발생했습니다."
            };
        }
    }
}
