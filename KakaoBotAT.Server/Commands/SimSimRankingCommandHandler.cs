using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !심랭킹 command to show top messages with most responses.
/// </summary>
public class SimSimRankingCommandHandler : ICommandHandler
{
    private readonly ISimSimService _simSimService;
    private readonly ILogger<SimSimRankingCommandHandler> _logger;

    public SimSimRankingCommandHandler(
        ISimSimService simSimService,
        ILogger<SimSimRankingCommandHandler> logger)
    {
        _simSimService = simSimService;
        _logger = logger;
    }

    public string Command => "!심랭킹";

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

            var topMessages = await _simSimService.GetTopMessagesAsync(limit);

            if (topMessages.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 등록된 심심이 메시지가 없습니다."
                };
            }

            var message = $"🏆 심심이 랭킹 TOP {limit}\n\n";
            for (int i = 0; i < topMessages.Count; i++)
            {
                var (msg, count) = topMessages[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{i + 1}."
                };

                var displayMsg = msg.Length > 20
                    ? msg.Substring(0, 17) + "..."
                    : msg;

                message += $"{medal} {displayMsg} ({count:N0}개)\n";
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SIMSIM_RANKING] Showing top {Limit} simsim messages for {Sender}",
                    limit, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIMSIM_RANKING] Error processing simsim ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "심심이 랭킹 조회 중 오류가 발생했습니다."
            };
        }
    }
}
