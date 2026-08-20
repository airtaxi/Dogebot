using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class OddEvenCommandHandler(ILogger<OddEvenCommandHandler> logger, IOddEvenService oddEvenService) : ICommandHandler
{
    public string Command => "!홀짝";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals("!홀", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("!짝", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var userChoice = data.Content.Trim().TrimStart('!');
            var message = oddEvenService.PlayOddEven(userChoice);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[ODDEVEN] User chose '{UserChoice}' for {Sender} in room {RoomId}", userChoice, data.SenderName, data.RoomId);

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

