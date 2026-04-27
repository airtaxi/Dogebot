using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !정보 command to display room information.
/// Shows room name, room ID, message count, user count, and sender hash.
/// </summary>
public class RoomInfoCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<RoomInfoCommandHandler> logger) : ICommandHandler
{
    public string Command => "!정보";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var (totalMessages, uniqueUsers) = await statisticsService.GetRoomStatisticsAsync(data.RoomId);

            var message = "ℹ️ 방 정보\n\n" +
                         $"방 이름: {data.RoomName}\n" +
                         $"방 ID: {data.RoomId}\n" +
                         $"총 메시지 수: {totalMessages:N0}개\n" +
                         $"감지된 인원 수: {uniqueUsers:N0}명\n" +
                         $"그룹채팅 여부: {(data.IsGroupChat ? "예" : "아니오")}\n\n" +
                         $"요청자 정보:\n" +
                         $"• 이름: {data.SenderName}\n" +
                         $"• 해시: {data.SenderHash}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[ROOM_INFO] Room info requested by {Sender} in room {RoomId}", 
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ROOM_INFO] Error processing room info command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "방 정보 조회 중 오류가 발생했습니다."
            };
        }
    }
}

