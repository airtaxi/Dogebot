using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !심삭제 command to delete simsim responses.
/// Admin only command.
/// Can delete specific message/response pair or all responses for a message.
/// </summary>
public class SimSimDeleteCommandHandler : ICommandHandler
{
    private readonly ISimSimService _simSimService;
    private readonly IAdminService _adminService;
    private readonly ILogger<SimSimDeleteCommandHandler> _logger;

    public SimSimDeleteCommandHandler(
        ISimSimService simSimService,
        IAdminService adminService,
        ILogger<SimSimDeleteCommandHandler> logger)
    {
        _simSimService = simSimService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!심삭제";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // Check if user is admin
            if (!await _adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다."
                };
            }

            var parts = data.Content.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🗑️ 사용법:\n" +
                             "• !심삭제 (메시지) - 메시지의 모든 답변 삭제\n" +
                             "• !심삭제 (메시지) / (답변) - 특정 답변만 삭제\n\n" +
                             "예시:\n" +
                             "• !심삭제 안녕\n" +
                             "• !심삭제 안녕 / 안녕하세요!"
                };
            }

            var content = parts[1];
            var splitIndex = content.IndexOf('/');

            // If no "/" separator, delete all responses for the message
            if (splitIndex == -1)
            {
                var message = content.Trim();

                if (string.IsNullOrWhiteSpace(message))
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "❌ 메시지는 비어있을 수 없습니다."
                    };
                }

                var deletedCount = await _simSimService.DeleteAllResponsesForMessageAsync(message);

                if (deletedCount == 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = $"❌ '{message}'에 대한 등록된 답변이 없습니다."
                    };
                }

                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[SIMSIM_DELETE] Admin {Sender} deleted ALL {Count} responses for message '{Message}'",
                        data.SenderName, deletedCount, message);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"✅ 삭제 완료!\n\n메시지: {message}\n삭제된 답변 수: {deletedCount}개"
                };
            }

            // Delete specific message/response pair
            var msg = content.Substring(0, splitIndex).Trim();
            var response = content.Substring(splitIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(msg) || string.IsNullOrWhiteSpace(response))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 메시지와 답변은 비어있을 수 없습니다."
                };
            }

            var deleted = await _simSimService.DeleteResponseAsync(msg, response);

            if (!deleted)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 해당 메시지/답변 조합을 찾을 수 없습니다.\n\n메시지: {msg}\n답변: {response}"
                };
            }

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[SIMSIM_DELETE] Admin {Sender} deleted '{Message}' / '{Response}'",
                    data.SenderName, msg, response);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 삭제 완료!\n\n메시지: {msg}\n답변: {response}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SIMSIM_DELETE] Error processing simsim delete command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "심심이 삭제 중 오류가 발생했습니다."
            };
        }
    }
}
