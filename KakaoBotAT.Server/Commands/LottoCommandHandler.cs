using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class LottoCommandHandler : ICommandHandler
{
    private readonly ILogger<LottoCommandHandler> _logger;
    private readonly Random _random = new();

    public LottoCommandHandler(ILogger<LottoCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!로또";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // 1부터 45까지 숫자를 셔플하여 6개 선택
            var numbers = Enumerable.Range(1, 45).OrderBy(_ => _random.Next()).Take(6).OrderBy(n => n).ToArray();

            var message = $"🎱 로또 번호\n{string.Join(", ", numbers)}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LOTTO] Generated numbers for {Sender} in room {RoomId}: {Numbers}", 
                    data.SenderName, data.RoomId, string.Join(", ", numbers));

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LOTTO] Error processing lotto command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "로또 번호 생성 중 오류가 발생했습니다."
            });
        }
    }
}
