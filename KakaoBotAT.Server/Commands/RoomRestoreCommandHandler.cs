using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RoomRestoreCommandHandler(
    IRoomMigrationService roomMigrationService,
    IAdminService adminService,
    ILogger<RoomRestoreCommandHandler> logger) : ICommandHandler
{
    public string Command => "!방복원";

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
            if (!await adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 관리자만 방 복원을 할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⚙️ 사용법:\n\n" +
                             "!방복원 (코드)\n\n" +
                             "💡 이전 방에서 !방백업 명령어로 코드를 먼저 발급받으세요."
                };
            }

            var code = parts[1].ToUpperInvariant();
            var result = await roomMigrationService.MigrateRoomDataAsync(code, data.RoomId, data.RoomName);

            if (!result.Success)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 방 복원 실패\n\n{result.ErrorMessage}"
                };
            }

            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("[ROOM_RESTORE] Room data restored from {SourceRoom} to {TargetRoom} ({Count} documents) by {Sender}",
                    result.SourceRoomName, data.RoomName, result.TotalDocumentsMigrated, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 방 복원 완료!\n\n" +
                         $"📋 원본: {result.SourceRoomName}\n" +
                         $"📋 대상: {data.RoomName}\n" +
                         $"📊 이전된 데이터: {result.TotalDocumentsMigrated:N0}건\n\n" +
                         $"통계, 설정, 반복 메시지가 모두 이전되었습니다."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ROOM_RESTORE] Error processing room restore command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "방 복원 중 오류가 발생했습니다."
            };
        }
    }
}
