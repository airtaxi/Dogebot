using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class RankCommandHandler : ICommandHandler
{
    private readonly IChatStatisticsService _statisticsService;
    private readonly ILogger<RankCommandHandler> _logger;

    public RankCommandHandler(
        IChatStatisticsService statisticsService,
        ILogger<RankCommandHandler> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    public string Command => "!랭크";

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
            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var limit = 10;

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedLimit))
            {
                limit = Math.Max(1, Math.Min(parsedLimit, 50));
            }

            var topMessages = await _statisticsService.GetTopMessagesAsync(data.RoomId, limit);

            if (topMessages.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 통계 데이터가 없습니다."
                };
            }

            var message = $"💬 많이 올라온 채팅 TOP {limit}\n\n";
            for (int i = 0; i < topMessages.Count; i++)
            {
                var (content, count) = topMessages[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{i + 1}."
                };
                
                var displayContent = content.Length > 30 
                    ? content.Substring(0, 27) + "..." 
                    : content;
                
                message += $"{medal} {displayContent} ({count:N0}회)\n";
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RANK] Showing top {Limit} messages for room {RoomId}", limit, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RANK] Error processing rank command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "랭크 조회 중 오류가 발생했습니다."
            };
        }
    }
}
