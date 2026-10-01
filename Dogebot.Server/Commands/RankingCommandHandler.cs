using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class RankingCommandHandler(IChatStatisticsService statisticsService, ILogger<RankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!랭킹";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var limit = 10;

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedLimit)) limit = Math.Max(1, Math.Min(parsedLimit, 50));

            var topUsers = await statisticsService.GetTopUsersAsync(data.RoomId, limit);

            if (topUsers.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 통계 데이터가 없습니다."
                };
            }

            var message = RankingMessageFormatter.FormatUserRanking($"📊 채팅 랭킹 TOP {limit}", topUsers);

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[RANKING] Showing top {Limit} users for room {RoomId}", limit, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[RANKING] Error processing ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
