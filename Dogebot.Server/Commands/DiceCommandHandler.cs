using Dogebot.Commons;

namespace Dogebot.Server.Commands;

public class DiceCommandHandler(ILogger<DiceCommandHandler> logger) : ICommandHandler
{
    private readonly Random _random = new();

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
                    Message = $"🎲 사용법: !주사위 (범위)\n예시: !주사위 100 → 1~100 사이의 랜덤 숫자\n최대 범위: {ulong.MaxValue:N0}"
                });
            }

            var rangeText = parts[1].Replace(",", string.Empty).Replace(".", string.Empty);

            if (!ulong.TryParse(rangeText, out ulong range) || range < 1)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 범위는 1 이상의 숫자여야 합니다."
                });
            }

            var result = NextUInt64(range) + 1;
            var message = $"🎲 주사위 (1~{range:N0})\n결과: {result:N0}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[DICE] Rolled 1~{Range} for {Sender} in room {RoomId}: {Result}", range, data.SenderName, data.RoomId, result);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DICE] Error processing dice command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "주사위 굴리기 중 오류가 발생했습니다."
            });
        }
    }

    // Returns a uniform value in [0, maxExclusive) across the full UInt64 range.
    private ulong NextUInt64(ulong maxExclusive)
    {
        if (maxExclusive <= 1) return 0;

        Span<byte> buffer = stackalloc byte[8];
        var limit = ulong.MaxValue - (ulong.MaxValue % maxExclusive);

        ulong value;
        do
        {
            _random.NextBytes(buffer);
            value = BitConverter.ToUInt64(buffer);
        } while (value >= limit);

        return value % maxExclusive;
    }
}

