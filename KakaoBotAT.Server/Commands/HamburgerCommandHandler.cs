using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !햄최몇 command to tell how many hamburgers a user can eat at once.
/// Returns a random number between 1-4.
/// </summary>
public class HamburgerCommandHandler : ICommandHandler
{
    private readonly ILogger<HamburgerCommandHandler> _logger;
    private readonly Random _random = new();

    public HamburgerCommandHandler(ILogger<HamburgerCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!햄최몇";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var count = _random.Next(1, 5); // 1~4
            var message = $"🍔 {data.SenderName}가 한번에 먹을 수 있는 햄버거의 갯수는 {count}개다 꿀꿀!";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[HAMBURGER] {Sender} can eat {Count} hamburgers at once in room {RoomId}",
                    data.SenderName, count, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HAMBURGER] Error processing hamburger command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "햄버거 개수 계산 중 오류가 발생했습니다."
            });
        }
    }
}
