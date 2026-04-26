using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RoomBackupCommandHandler(
    IRoomMigrationService roomMigrationService,
    IAdminService adminService,
    ILogger<RoomBackupCommandHandler> logger) : ICommandHandler
{
    public string Command => "!방백업";

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
                    Message = "⛔ 권한이 없습니다. 관리자만 방 백업을 할 수 있습니다."
                };
            }

            var code = await roomMigrationService.CreateMigrationCodeAsync(
                data.RoomId, data.RoomName, data.SenderHash, data.SenderName);

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("[ROOM_BACKUP] Migration code {Code} created for room {RoomName} by {Sender}",
                    code, data.RoomName, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 방 백업 코드 생성 완료!\n\n" +
                         $"🔑 코드: {code}\n" +
                         $"📋 방: {data.RoomName}\n" +
                         $"⏳ 유효시간: 10분\n\n" +
                         $"새 방에서 아래 명령어를 입력하세요:\n" +
                         $"!방복원 {code}\n\n" +
                         $"⚠️ 모든 통계, 설정, 반복 메시지가 이전됩니다.\n" +
                         $"이전 후 이 방의 데이터는 삭제됩니다."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ROOM_BACKUP] Error processing room backup command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "방 백업 중 오류가 발생했습니다."
            };
        }
    }
}
