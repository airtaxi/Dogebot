using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxNotificationRemoveCommandHandler(
    IImaxNotificationService imaxNotificationService,
    IAdminService adminService,
    ILogger<ImaxNotificationRemoveCommandHandler> logger) : ICommandHandler
{
    public string Command => "!용아맥해제";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 용아맥 알림을 해제할 수 있습니다."
                };
            }

            var notification = await imaxNotificationService.GetNotificationAsync(data.RoomId);
            if (notification is null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "ℹ️ 이 방에 등록된 용아맥 알림이 없습니다."
                };
            }

            await imaxNotificationService.RemoveNotificationAsync(data.RoomId);
            var dateDisplay = ImaxNotificationService.FormatScreeningDate(notification.ScreeningDate);

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("[IMAX_REMOVE] IMAX notification removed from room {RoomName} by {Sender}",
                    data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 용아맥 알림이 해제되었습니다.\n📅 {dateDisplay}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IMAX_REMOVE] Error processing IMAX notification remove command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "용아맥 알림 해제 중 오류가 발생했습니다."
            };
        }
    }
}
