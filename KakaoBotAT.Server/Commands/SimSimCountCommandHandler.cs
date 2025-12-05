using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !심몇개 command to show how many responses exist for a message.
/// </summary>
public class SimSimCountCommandHandler : ICommandHandler
{
    private readonly ISimSimService _simSimService;
    private readonly ILogger<SimSimCountCommandHandler> _logger;

    public SimSimCountCommandHandler(
        ISimSimService simSimService,
        ILogger<SimSimCountCommandHandler> logger)
    {
        _simSimService = simSimService;
        _logger = logger;
    }

    public string Command => "!심몇개";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🔢 사용법: !심몇개 (메시지)\n예시: !심몇개 안녕"
                };
            }

            var message = parts[1];
            var count = await _simSimService.GetResponseCountAsync(message);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SIMSIM_COUNT] Message '{Message}' has {Count} responses, queried by {Sender}",
                    message, count, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"📊 '{message}'에 대한 답변 개수: {count}개"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIMSIM_COUNT] Error processing simsim count command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "심심이 개수 조회 중 오류가 발생했습니다."
            };
        }
    }
}
