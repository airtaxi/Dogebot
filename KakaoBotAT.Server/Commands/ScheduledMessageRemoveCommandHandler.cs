using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ScheduledMessageRemoveCommandHandler : ICommandHandler
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IAdminService _adminService;
    private readonly ILogger<ScheduledMessageRemoveCommandHandler> _logger;

    public ScheduledMessageRemoveCommandHandler(
        IScheduledMessageService scheduledMessageService,
        IAdminService adminService,
        ILogger<ScheduledMessageRemoveCommandHandler> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!반복해제";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 반복 메시지를 해제할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // !반복해제 (no args) → show list + usage
            if (parts.Length == 1)
            {
                var messages = await _scheduledMessageService.GetScheduledMessagesAsync(data.RoomId);
                if (messages.Count == 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "ℹ️ 이 방에 설정된 반복 메시지가 없습니다."
                    };
                }

                var list = FormatScheduledMessageList(messages);
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = list + "\n" +
                             "━━━━━━━━━━━━━━━━━━\n" +
                             "🗑️ 삭제 방법:\n" +
                             "• !반복해제 (번호) - 특정 메시지 삭제\n" +
                             "• !반복해제 전체 - 모든 메시지 삭제"
                };
            }

            var arg = parts[1];

            // !반복해제 전체
            if (arg.Equals("전체", StringComparison.OrdinalIgnoreCase))
            {
                var deletedCount = await _scheduledMessageService.RemoveAllScheduledMessagesAsync(data.RoomId);

                if (deletedCount == 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "ℹ️ 이 방에 설정된 반복 메시지가 없습니다."
                    };
                }

                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[SCHEDULED_REMOVE] All {Count} scheduled messages removed from room {RoomName} by {Sender}",
                        deletedCount, data.RoomName, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"✅ 반복 메시지 {deletedCount}개가 모두 삭제되었습니다."
                };
            }

            // !반복해제 (번호)
            if (!int.TryParse(arg, out var index) || index < 1)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 유효하지 않은 번호입니다.\n\n" +
                             "사용법:\n" +
                             "• !반복해제 (번호)\n" +
                             "• !반복해제 전체\n\n" +
                             "!반복목록으로 번호를 확인할 수 있습니다."
                };
            }

            var success = await _scheduledMessageService.RemoveScheduledMessageAsync(data.RoomId, index);
            if (!success)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ {index}번 반복 메시지를 찾을 수 없습니다.\n\n" +
                             "!반복목록으로 번호를 확인해주세요."
                };
            }

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[SCHEDULED_REMOVE] Scheduled message #{Index} removed from room {RoomName} by {Sender}",
                    index, data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ {index}번 반복 메시지가 삭제되었습니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SCHEDULED_REMOVE] Error processing scheduled message remove command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "반복 메시지 해제 중 오류가 발생했습니다."
            };
        }
    }

    private static string FormatScheduledMessageList(List<Models.ScheduledMessage> messages)
    {
        var result = "📋 반복 메시지 목록\n\n";
        for (int i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            var preview = message.Message.Length > 30
                ? message.Message[..27] + "..."
                : message.Message;
            // Replace newlines for preview
            preview = preview.Replace("\n", " ").Replace("\r", "");
            var hoursDisplay = string.Join(", ", message.Hours.Select(h => $"{h}시"));
            result += $"{i + 1}. \"{preview}\"\n   ⏰ {hoursDisplay} | 👤 {message.CreatedByName}\n\n";
        }
        return result.TrimEnd();
    }
}
