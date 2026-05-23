using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class RankingCommandHandler(IChatStatisticsService statisticsService, ILogger<RankingCommandHandler> logger) : ICommandHandler
{
    private const string WordJoiner = "\u2060";

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

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedLimit))
                limit = Math.Max(1, Math.Min(parsedLimit, 50));

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

            var message = $"📊 채팅 랭킹 TOP {limit}\n\n";
            for (var index = 0; index < topUsers.Count; index++)
            {
                var (senderName, messageCount) = topUsers[index];
                var medal = index switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{index + 1}."
                };

                message += $"{medal} {InsertWordJoiners(senderName)}: {messageCount:N0}회\n";
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[RANKING] Showing top {Limit} users for room {RoomId}", limit, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
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

    private static string InsertWordJoiners(string value) =>
        value.Length <= 1 ? value : string.Join(WordJoiner, value.Select(character => character.ToString()));
}
