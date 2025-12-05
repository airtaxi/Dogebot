using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !선택 command to randomly pick one option from user-provided choices.
/// Usage: !선택 (option1) (option2) (option3) ...
/// </summary>
public class ChoiceCommandHandler : ICommandHandler
{
    private readonly ILogger<ChoiceCommandHandler> _logger;
    private readonly Random _random = new();

    public ChoiceCommandHandler(ILogger<ChoiceCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!선택";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🤔 사용법: !선택 (선택지1) (선택지2) ...\n" +
                             "예시: !선택 치킨 피자 햄버거\n\n" +
                             "최소 2개 이상의 선택지를 입력해주세요!"
                });
            }

            // Skip the command itself and get all choices
            var choices = parts.Skip(1).ToArray();
            var selected = choices[_random.Next(choices.Length)];

            var message = $"🎯 선택 결과: {selected}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[CHOICE] Selected '{Selected}' from {ChoiceCount} options for {Sender} in room {RoomId}",
                    selected, choices.Length, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CHOICE] Error processing choice command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "선택 중 오류가 발생했습니다."
            });
        }
    }
}
