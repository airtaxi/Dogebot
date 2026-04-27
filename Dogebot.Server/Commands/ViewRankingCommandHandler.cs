using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class ViewRankingCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<ViewRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!조회";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "사용법: !조회 (roomId)\n" +
                             $"예시: !조회 {data.RoomId}"
                };
            }

            var targetRoomId = parts[1];
            var topUsers = await statisticsService.GetTopUsersAsync(targetRoomId, 10);

            if (topUsers.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 통계 데이터가 없습니다."
                };
            }

            var message = "📊 채팅 랭킹 TOP 10\n\n";
            for (int i = 0; i < topUsers.Count; i++)
            {
                var (senderName, messageCount) = topUsers[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{i + 1}."
                };
                message += $"{medal} {senderName}: {messageCount:N0}회\n";
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[VIEW_RANKING] Showing rankings for room {TargetRoomId} requested by {Sender} in room {RoomId}", 
                    targetRoomId, data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VIEW_RANKING] Error processing view ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}

