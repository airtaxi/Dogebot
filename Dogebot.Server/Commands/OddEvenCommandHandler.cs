using Dogebot.Commons;

namespace Dogebot.Server.Commands;

public class OddEvenCommandHandler(ILogger<OddEvenCommandHandler> logger) : ICommandHandler
{
    private readonly Random _random = new();

    public string Command => "!홀짝";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals("!홀", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("!짝", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var result = _random.Next(0, 2) == 0 ? "홀" : "짝";
            var userChoice = data.Content.Trim().TrimStart('!');
            
            var isWin = userChoice.Equals(result, StringComparison.OrdinalIgnoreCase);
            var message = $"🎲 결과: {result}\n{(isWin ? "✅ 맞췄습니다!" : "❌ 틀렸습니다!")}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[ODDEVEN] User chose '{UserChoice}', result was '{Result}' ({WinLose}) for {Sender} in room {RoomId}", 
                    userChoice, result, isWin ? "WIN" : "LOSE", data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ODDEVEN] Error processing odd/even command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "홀짝 게임 중 오류가 발생했습니다."
            });
        }
    }
}

