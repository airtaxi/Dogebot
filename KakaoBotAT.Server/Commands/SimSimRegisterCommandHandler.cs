using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !심등록 command to register a new simsim response.
/// Only works in private chats (not group chats).
/// </summary>
public class SimSimRegisterCommandHandler : ICommandHandler
{
    private readonly ISimSimService _simSimService;
    private readonly ILogger<SimSimRegisterCommandHandler> _logger;

    public SimSimRegisterCommandHandler(
        ISimSimService simSimService,
        ILogger<SimSimRegisterCommandHandler> logger)
    {
        _simSimService = simSimService;
        _logger = logger;
    }

    public string Command => "!심등록";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // Check if this is a group chat
            if (data.IsGroupChat)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⚠️ !심등록은 개인톡에서만 사용 가능합니다.\n" +
                             "채팅창이 너무 시끄러워지는 것을 방지하기 위함입니다."
                };
            }

            var parts = data.Content.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "💾 사용법: !심등록 (메시지) / (답변)\n" +
                             "예시: !심등록 안녕 / 안녕하세요!"
                };
            }

            var content = parts[1];
            var splitIndex = content.IndexOf('/');

            if (splitIndex == -1)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 잘못된 형식입니다.\n\n" +
                             "사용법: !심등록 (메시지) / (답변)\n" +
                             "예시: !심등록 안녕 / 안녕하세요!"
                };
            }

            var message = content.Substring(0, splitIndex).Trim();
            var response = content.Substring(splitIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(response))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 메시지와 답변은 비어있을 수 없습니다."
                };
            }

            await _simSimService.AddResponseAsync(message, response, data.SenderHash);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[SIMSIM_REGISTER] {Sender} registered '{Message}' / '{Response}'",
                    data.SenderName, message, response);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 등록 완료!\n\n메시지: {message}\n답변: {response}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIMSIM_REGISTER] Error processing simsim register command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "심심이 등록 중 오류가 발생했습니다."
            };
        }
    }
}
