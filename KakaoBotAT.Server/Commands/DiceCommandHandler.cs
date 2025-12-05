using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class DiceCommandHandler : ICommandHandler
{
    private readonly ILogger<DiceCommandHandler> _logger;
    private readonly Random _random = new();

    public DiceCommandHandler(ILogger<DiceCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!주사위";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🎲 사용법: !주사위 (범위)\n예시: !주사위 100 → 1~100 사이의 랜덤 숫자"
                });
            }

            if (!int.TryParse(parts[1], out int range) || range < 1)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 범위는 1 이상의 숫자여야 합니다."
                });
            }

            if (range > 1000000)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 범위는 최대 1,000,000까지 가능합니다."
                });
            }

            var result = _random.Next(1, range + 1);
            var message = $"🎲 주사위 (1~{range})\n결과: {result}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[DICE] Rolled 1~{Range} for {Sender} in room {RoomId}: {Result}", 
                    range, data.SenderName, data.RoomId, result);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DICE] Error processing dice command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "주사위 굴리기 중 오류가 발생했습니다."
            });
        }
    }
}
