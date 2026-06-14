using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class LottoCommandHandler(ILottoService lottoService, ILogger<LottoCommandHandler> logger) : ICommandHandler
{
    public string Command => "!로또";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var count = 1;

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedCount))
            {
                count = Math.Max(1, Math.Min(parsedCount, 10));
            }

            var message = lottoService.CreateLottoMessage(count);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[LOTTO] Generated {Count} set(s) for {Sender} in room {RoomId}", count, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LOTTO] Error processing lotto command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "로또 번호 생성 중 오류가 발생했습니다."
            });
        }
    }
}

