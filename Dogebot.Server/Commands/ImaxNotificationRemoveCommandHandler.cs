using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class ImaxNotificationRemoveCommandHandler(IImaxNotificationService imaxNotificationService, IAdminService adminService, ILogger<ImaxNotificationRemoveCommandHandler> logger) : ICommandHandler
{
    public string Command => "!아이맥스해제";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("!용아맥해제", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("!용아맥해제 ", StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 IMAX 알림을 해제할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // !아이맥스해제 (no args) → show list + usage
            if (parts.Length == 1)
            {
                var notifications = await imaxNotificationService.GetNotificationsAsync(data.RoomId);
                if (notifications.Count == 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "ℹ️ 이 방에 등록된 IMAX 알림이 없습니다."
                    };
                }

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = FormatNotificationList(notifications) + "\n" +
                             "━━━━━━━━━━━━━━━━━━\n" +
                             "🗑️ 해제 방법:\n" +
                             "• !아이맥스해제 (번호) - 특정 알림 해제\n" +
                             "• !아이맥스해제 전체 - 모든 알림 해제"
                };
            }

            var arg = parts[1];

            // !아이맥스해제 전체
            if (arg.Equals("전체", StringComparison.OrdinalIgnoreCase))
            {
                var deletedCount = await imaxNotificationService.RemoveAllNotificationsAsync(data.RoomId);

                if (deletedCount == 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "ℹ️ 이 방에 등록된 IMAX 알림이 없습니다."
                    };
                }

                if (logger.IsEnabled(LogLevel.Warning))
                    logger.LogWarning("[IMAX_REMOVE] All {Count} IMAX notifications removed from room {RoomName} by {Sender}", deletedCount, data.RoomName, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"✅ IMAX 알림 {deletedCount}개가 모두 해제되었습니다."
                };
            }

            // !아이맥스해제 (번호)
            if (!int.TryParse(arg, out var index) || index < 1)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 유효하지 않은 번호입니다.\n\n" +
                             "사용법:\n" +
                             "• !아이맥스해제 (번호)\n" +
                             "• !아이맥스해제 전체\n\n" +
                             "!아이맥스목록으로 번호를 확인할 수 있습니다."
                };
            }

            var removed = await imaxNotificationService.RemoveNotificationAsync(data.RoomId, index);
            if (removed is null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ {index}번 IMAX 알림을 찾을 수 없습니다.\n\n" +
                             "!아이맥스목록으로 번호를 확인해주세요."
                };
            }

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("[IMAX_REMOVE] IMAX notification #{Index} removed from room {RoomName} by {Sender}", index, data.RoomName, data.SenderName);

            var dateDisplay = ImaxNotificationService.FormatScreeningDate(removed.ScreeningDate);
            var siteDisplay = string.IsNullOrEmpty(removed.SiteName) ? "" : $"\n🏢 CGV {removed.SiteName}";

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ {index}번 IMAX 알림이 해제되었습니다.{siteDisplay}\n🎬 {removed.MovieName}\n📅 {dateDisplay}"
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[IMAX_REMOVE] Error processing IMAX notification remove command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "IMAX 알림 해제 중 오류가 발생했습니다."
            };
        }
    }

    private static string FormatNotificationList(List<Models.ImaxNotification> notifications)
    {
        var result = "🔔 IMAX 알림 목록\n\n";
        for (int i = 0; i < notifications.Count; i++)
        {
            var notification = notifications[i];
            var dateDisplay = ImaxNotificationService.FormatScreeningDate(notification.ScreeningDate);
            var siteName = string.IsNullOrEmpty(notification.SiteName) ? "용산아이파크몰" : notification.SiteName;
            var statusDisplay = notification.PendingMessage is not null ? "🟢 IMAX 감지됨" : "🔍 대기 중";
            result += $"{i + 1}. 🎬 {notification.MovieName}\n";
            result += $"   🏢 CGV {siteName} | 📅 {dateDisplay} | {statusDisplay}\n\n";
        }
        return result.TrimEnd();
    }
}
