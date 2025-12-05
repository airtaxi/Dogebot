using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !정보 command to display room information.
/// Shows room name, room ID, message count, user count, and sender hash.
/// </summary>
public class RoomInfoCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<RoomInfoCommandHandler> _logger;

    public RoomInfoCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<RoomInfoCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!정보";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var (totalMessages, uniqueUsers) = await _statisticsService.GetRoomStatisticsAsync(data.RoomId);

            var message = "ℹ️ 방 정보\n\n" +
                         $"방 이름: {data.RoomName}\n" +
                         $"방 ID: {data.RoomId}\n" +
                         $"총 메시지 수: {totalMessages:N0}개\n" +
                         $"감지된 인원 수: {uniqueUsers:N0}명\n" +
                         $"그룹채팅 여부: {(data.IsGroupChat ? "예" : "아니오")}\n\n" +
                         $"요청자 정보:\n" +
                         $"• 이름: {data.SenderName}\n" +
                         $"• 해시: {data.SenderHash}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[ROOM_INFO] Room info requested by {Sender} in room {RoomId}", 
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
            _logger.LogError(ex, "[ROOM_INFO] Error processing room info command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "방 정보 조회 중 오류가 발생했습니다."
            };
        }
    }
}
