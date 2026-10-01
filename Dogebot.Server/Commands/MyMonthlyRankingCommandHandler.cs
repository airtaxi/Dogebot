using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !내월간랭킹 command, showing the sender's own rank over the last 28 days.
/// </summary>
public class MyMonthlyRankingCommandHandler(IChatStatisticsService statisticsService, ILogger<MyMonthlyRankingCommandHandler> logger) : ICommandHandler
{
    private const int WindowDays = 28;

    public string Command => "!내월간랭킹";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var toUtc = DateTimeOffset.UtcNow;
            var fromUtc = toUtc.AddDays(-WindowDays);
            var result = await statisticsService.GetUserRankByPeriodAsync(data.RoomId, data.SenderHash, fromUtc, toUtc);

            if (result == null) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"{data.SenderName}님의 최근 {WindowDays}일 채팅 기록이 없습니다."};

            var (rank, messageCount) = result.Value;
            var rankEmoji = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => "📊"
            };

            var message = $"{rankEmoji} {data.SenderName}님의 월간 랭킹 (최근 {WindowDays}일)\n순위: {rank}위\n채팅 수: {messageCount:N0}회";

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[MY_MONTHLY_RANKING] User {SenderName} is rank {Rank} with {Count} messages in room {RoomId}", data.SenderName, rank, messageCount, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[MY_MONTHLY_RANKING] Error processing my monthly ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "월간 랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
