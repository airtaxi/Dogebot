using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxScheduleQueryCommandHandler(
    IImaxNotificationService imaxNotificationService,
    ILogger<ImaxScheduleQueryCommandHandler> logger) : ICommandHandler
{
    public string Command => "!아이맥스조회";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("!용아맥조회 ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("!용아맥조회", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var movieSearchQuery = parts.Length > 1 ? parts[1].Trim() : null;

            imaxNotificationService.StartSession(
                data.RoomId, data.SenderHash, data.SenderName, data.RoomName,
                ImaxSessionType.ScheduleQuery, movieSearchQuery);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_QUERY] Session started by {Sender} in room {RoomName}",
                    data.SenderName, data.RoomName);

            var regionList = string.Join("\n", [
                "  1. 서울",
                "  2. 경기",
                "  3. 인천",
                "  4. 강원",
                "  5. 대전/충청",
                "  6. 대구",
                "  7. 부산/울산",
                "  8. 경상",
                "  9. 광주/전라"
            ]);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "🔍 IMAX 시간표 조회\n\n" +
                          "지역을 선택해주세요:\n" +
                          regionList + "\n\n" +
                          "숫자를 입력해주세요.\n\n" +
                          "❌ 취소: !취소\n" +
                          "⏳ 5분 내에 입력하지 않으면 자동 취소됩니다."
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[IMAX_QUERY] Error starting schedule query session");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "시간표 조회 시작 중 오류가 발생했습니다."
            });
        }
    }
}
