using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the 심심아 command to get a random response for a message.
/// </summary>
public class SimSimQueryCommandHandler : ICommandHandler
{
    private readonly ISimSimService _simSimService;
    private readonly ILogger<SimSimQueryCommandHandler> _logger;
    private readonly Random _random = new();

    public SimSimQueryCommandHandler(
        ISimSimService simSimService,
        ILogger<SimSimQueryCommandHandler> logger)
    {
        _simSimService = simSimService;
        _logger = logger;
    }

    public string Command => "심심아";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "💬 사용법: 심심아 (메시지)\n예시: 심심아 안녕"
                };
            }

            var message = parts[1];
            var responses = await _simSimService.GetResponsesAsync(message);

            if (responses.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{message}'에 대한 등록된 답변이 없습니다.\n\n" +
                             "개인톡에서 !심등록 (메시지) / (답변) 으로 답변을 추가해주세요!"
                };
            }

            var randomResponse = responses[_random.Next(responses.Count)];

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SIMSIM_QUERY] Message '{Message}' got response '{Response}' for {Sender} in room {RoomId}",
                    message, randomResponse, data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = randomResponse
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIMSIM_QUERY] Error processing simsim query command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "심심이 조회 중 오류가 발생했습니다."
            };
        }
    }
}
