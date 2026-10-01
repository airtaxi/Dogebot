using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !주간랭킹 command, showing the chat ranking over the last 7 days.
/// </summary>
public class WeeklyRankingCommandHandler(IChatStatisticsService statisticsService, ILogger<WeeklyRankingCommandHandler> logger) : ICommandHandler
{
    private const int WindowDays = 7;

    public string Command => "!주간랭킹";

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

            var toUtc = DateTimeOffset.UtcNow;
            var fromUtc = toUtc.AddDays(-WindowDays);
            var topUsers = await statisticsService.GetTopUsersByPeriodAsync(data.RoomId, fromUtc, toUtc, limit);

            if (topUsers.Count == 0) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"최근 {WindowDays}일간 통계 데이터가 없습니다."};

            var message = RankingMessageFormatter.FormatUserRanking($"📊 주간 채팅 랭킹 (최근 {WindowDays}일) TOP {limit}", topUsers);

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[WEEKLY_RANKING] Showing top {Limit} users for room {RoomId}", limit, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[WEEKLY_RANKING] Error processing weekly ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "주간 랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
