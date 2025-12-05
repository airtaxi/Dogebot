using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class JudgeCommandHandler : ICommandHandler
{
    private readonly ILogger<JudgeCommandHandler> _logger;
    private readonly Random _random = new();

    public JudgeCommandHandler(ILogger<JudgeCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "판사님";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var verdictType = _random.Next(0, 5);
            string verdict = verdictType switch
            {
                0 => "유죄",
                1 => "무죄",
                2 => $"집행유예 {_random.Next(1, 81)}년",
                3 => "사형",
                4 => $"징역 {_random.Next(1, 81)}년",
                _ => "무죄"
            };

            var message = $"⚖️ 판결: {verdict}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[JUDGE] Verdict '{Verdict}' for {Sender} in room {RoomId}", 
                    verdict, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[JUDGE] Error processing judge command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "판결 중 오류가 발생했습니다."
            });
        }
    }
}
