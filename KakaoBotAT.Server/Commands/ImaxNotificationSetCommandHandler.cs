using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxNotificationSetCommandHandler(
    IImaxNotificationService imaxNotificationService,
    IAdminService adminService,
    ILogger<ImaxNotificationSetCommandHandler> logger) : ICommandHandler
{
    public string Command => "!아이맥스설정";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("!용아맥설정", StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 IMAX 알림을 설정할 수 있습니다."
                };
            }

            // Check if room already has an active notification
            var existing = await imaxNotificationService.GetNotificationAsync(data.RoomId);
            if (existing is not null)
            {
                var dateDisplay = ImaxNotificationService.FormatScreeningDate(existing.ScreeningDate);
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 이 방에 이미 알림이 등록되어 있습니다.\n\n" +
                              $"🏢 CGV {existing.SiteName}\n" +
                              $"🎬 {existing.MovieName}\n" +
                              $"📅 {dateDisplay}\n\n" +
                              $"!아이맥스해제 후 다시 등록해주세요."
                };
            }

            imaxNotificationService.StartSession(data.RoomId, data.SenderHash, data.SenderName, data.RoomName);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_SET] Session started by {Sender} in room {RoomName}",
                    data.SenderName, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "📝 IMAX 알림 설정\n\n" +
                          "알림을 받을 영화 이름을 입력해주세요.\n" +
                          "예: 아바타\n\n" +
                          "❌ 취소: !취소\n" +
                          "⏳ 5분 내에 입력하지 않으면 자동 취소됩니다."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[IMAX_SET] Error processing IMAX notification set command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "IMAX 알림 설정 중 오류가 발생했습니다."
            };
        }
    }
}
