using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class ImaxNotificationRemoveCommandHandler(
    IImaxNotificationService imaxNotificationService,
    IAdminService adminService,
    ILogger<ImaxNotificationRemoveCommandHandler> logger) : ICommandHandler
{
    public string Command => "!아이맥스해제";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("!용아맥해제", StringComparison.OrdinalIgnoreCase);
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

            var notification = await imaxNotificationService.GetNotificationAsync(data.RoomId);
            if (notification is null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이 방에 등록된 IMAX 알림이 없습니다."
                };
            }

            await imaxNotificationService.RemoveNotificationAsync(data.RoomId);
            var dateDisplay = ImaxNotificationService.FormatScreeningDate(notification.ScreeningDate);
            var siteDisplay = string.IsNullOrEmpty(notification.SiteName) ? "" : $"\n🏢 CGV {notification.SiteName}";

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("[IMAX_REMOVE] IMAX notification removed from room {RoomName} by {Sender}",
                    data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ IMAX 알림이 해제되었습니다.{siteDisplay}\n🎬 {notification.MovieName}\n📅 {dateDisplay}"
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
}

