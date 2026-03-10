using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class ProbabilityCommandHandler(ILogger<ProbabilityCommandHandler> logger) : ICommandHandler
{
    private readonly Random _random = new();

    public string Command => "확률";

    public bool CanHandle(string content)
    {
        return content.Contains("확률", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var probability = _random.Next(0, 101);
            var message = $"확률: {probability}%";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[PROBABILITY] Generated {Probability}% for {Sender} in room {RoomId}", 
                    probability, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PROBABILITY] Error processing probability command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "확률 계산 중 오류가 발생했습니다."
            });
        }
    }
}
