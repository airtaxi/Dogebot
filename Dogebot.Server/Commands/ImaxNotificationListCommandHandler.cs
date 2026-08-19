using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class ImaxNotificationListCommandHandler(IImaxNotificationService imaxNotificationService, IAdminService adminService, ILogger<ImaxNotificationListCommandHandler> logger) : ICommandHandler
{
    public string Command => "!아이맥스목록";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) || trimmed.Equals("!용아맥목록", StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 IMAX 알림을 조회할 수 있습니다."
                };
            }

            var notifications = await imaxNotificationService.GetNotificationsAsync(data.RoomId);

            if (notifications.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이 방에 등록된 IMAX 알림이 없습니다.\n\n" +
                             "!아이맥스설정으로 등록할 수 있습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_LIST] Showing {Count} IMAX notifications for room {RoomName}", notifications.Count, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatNotificationList(notifications)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[IMAX_LIST] Error processing IMAX notification list command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "IMAX 알림 조회 중 오류가 발생했습니다."
            };
        }
    }

    private static string FormatNotificationList(List<Models.ImaxNotification> notifications)
    {
        var result = $"🔔 IMAX 알림 목록 ({notifications.Count}개)\n\n";
        for (int i = 0; i < notifications.Count; i++)
        {
            var notification = notifications[i];
            var dateDisplay = ImaxNotificationService.FormatScreeningDate(notification.ScreeningDate);
            var keywordDisplay = string.IsNullOrEmpty(notification.Keyword) ? "없음" : notification.Keyword;
            var statusDisplay = notification.PendingMessage is not null ? "🟢 IMAX 감지됨 (다음 메시지에 알림 전송)" : "🔍 대기 중 (IMAX 미감지)";
            var siteName = string.IsNullOrEmpty(notification.SiteName) ? "용산아이파크몰" : notification.SiteName;

            result += $"{i + 1}. 🎬 {notification.MovieName}\n";
            result += $"   🏢 CGV {siteName}\n";
            result += $"   📅 {dateDisplay}\n";
            result += $"   🔑 키워드: {keywordDisplay}\n";
            result += $"   📊 {statusDisplay}\n";
            result += $"   👤 등록자: {notification.CreatedByName}\n\n";
        }

        return result.TrimEnd() + "\n\n" +
               "ℹ️ 5~10초 간격으로 IMAX 상영 여부를 확인합니다.\n" +
               "!아이맥스해제 (번호)로 특정 알림을 해제할 수 있습니다.";
    }
}
