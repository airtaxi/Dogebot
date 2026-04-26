using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ScheduledMessageListCommandHandler(
    IScheduledMessageService scheduledMessageService,
    IAdminService adminService,
    ILogger<ScheduledMessageListCommandHandler> logger) : ICommandHandler
{
    public string Command => "!반복목록";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 관리자만 반복 메시지 목록을 조회할 수 있습니다."
                };
            }

            var messages = await scheduledMessageService.GetScheduledMessagesAsync(data.RoomId);

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
                var daysDisplay = FormatDays(message.Days);
                var hoursDisplay = string.Join(", ", message.Hours.Select(h => $"{h}시"));
                result += $"{i + 1}. \"{preview}\"\n   📅 {daysDisplay} | ⏰ {hoursDisplay} | 👤 {message.CreatedByName}\n\n";
            }

            result = result.TrimEnd();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[SCHEDULED_LIST] Showing {Count} scheduled messages for room {RoomName}",
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
            logger.LogError(ex, "[SCHEDULED_LIST] Error processing scheduled message list command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "반복 메시지 목록 조회 중 오류가 발생했습니다."
            };
        }
    }

    private static string FormatDays(List<int> days)
    {
        if (days.Count == 7)
            return "전체";

        var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };
        return string.Join(", ", days.Order().Select(d => dayNames[d]));
    }
}
