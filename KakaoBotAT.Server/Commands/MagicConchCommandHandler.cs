using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class MagicConchCommandHandler(ILogger<MagicConchCommandHandler> logger) : ICommandHandler
{
    private readonly Random _random = new();

    private static readonly string[] Answers =
    [
        "그래",
        "안 돼",
        "절대 안 돼",
        "무조건이야",
        "언젠가는",
        "다시 물어봐",
        "아마도",
        "절대로",
        "당연하지",
        "생각해보지도 마",
        "좋은 생각이야",
        "별로야"
    ];

    public string Command => "소라고동님";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var answer = Answers[_random.Next(Answers.Length)];
            var message = $"🐚 소라고동님: {answer}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[MAGIC_CONCH] Answer '{Answer}' for {Sender} in room {RoomId}", 
                    answer, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MAGIC_CONCH] Error processing magic conch command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "소라고동님이 응답하지 않습니다."
            });
        }
    }
}
