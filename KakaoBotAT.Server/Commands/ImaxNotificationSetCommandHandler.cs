using System.Globalization;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxNotificationSetCommandHandler(
    IImaxNotificationService imaxNotificationService,
    IAdminService adminService,
    ILogger<ImaxNotificationSetCommandHandler> logger) : ICommandHandler
{
    public string Command => "!용아맥설정";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 용아맥 알림을 설정할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 사용법: !용아맥설정 (날짜) [구문]\n\n" +
                             "예: !용아맥설정 20260330\n" +
                             "예: !용아맥설정 20260330 IMAX알림\n\n" +
                             "날짜는 yyyyMMdd 형식으로 입력하세요.\n" +
                             "[구문]은 카카오톡 키워드 알림용이며 생략 가능합니다."
                };
            }

            var dateStr = parts[1];
            if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 날짜 형식이 올바르지 않습니다.\nyyyyMMdd 형식으로 입력하세요. (예: 20260330)"
                };
            }

            var kstNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
            if (parsedDate.Date < kstNow.Date)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 과거 날짜는 설정할 수 없습니다."
                };
            }

            // Optional keyword: everything after the date
            string? keyword = parts.Length > 2
                ? string.Join(' ', parts[2..])
                : null;

            var (success, message) = await imaxNotificationService.RegisterAsync(
                data.RoomId, dateStr, keyword, data.SenderHash, data.SenderName, data.RoomName);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_SET] {Result} by {Sender} in room {RoomName} for date {Date}",
                    success ? "Registered" : "Failed", data.SenderName, data.RoomName, dateStr);

            // In personal chat, skip reply on success to preserve reply capability for the actual notification
            if (success && !data.IsGroupChat)
                return new ServerResponse();

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IMAX_SET] Error processing IMAX notification set command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "용아맥 알림 설정 중 오류가 발생했습니다."
            };
        }
    }
}
