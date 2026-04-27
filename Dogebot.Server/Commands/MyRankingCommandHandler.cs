using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class MyRankingCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<MyRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!내랭킹";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var result = await statisticsService.GetUserRankAsync(data.RoomId, data.SenderHash);

            if (result == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"{data.SenderName}님의 채팅 기록이 없습니다."
                };
            }

            var (rank, messageCount) = result.Value;
            var rankEmoji = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => "📊"
            };

            var message = $"{rankEmoji} {data.SenderName}님의 랭킹\n순위: {rank}위\n채팅 수: {messageCount:N0}회";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[MY_RANKING] User {SenderName} is rank {Rank} with {Count} messages in room {RoomId}", 
                    data.SenderName, rank, messageCount, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MY_RANKING] Error processing my ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}

