using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class AdminRemoveCommandHandler : ICommandHandler
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminRemoveCommandHandler> _logger;

    public AdminRemoveCommandHandler(
        IAdminService adminService,
        ILogger<AdminRemoveCommandHandler> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!관리제거";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (data.SenderHash != _adminService.ChiefAdminHash)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 최고 관리자만 제거할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🗑️ 사용법:\n" +
                             "!관리제거 (SenderHash)\n\n" +
                             "예시:\n" +
                             "!관리제거 abc123def456..."
                };
            }

            var senderHash = parts[1];
            var removed = await _adminService.RemoveAdminAsync(senderHash, data.SenderHash);

            if (!removed)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[ADMIN_REMOVE] Failed to remove admin {Hash} by {Sender}",
                        senderHash, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 제거 실패\n\n" +
                             "• 해당 SenderHash는 관리자가 아니거나\n" +
                             "• 최고 관리자는 제거할 수 없습니다."
                };
            }

            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[ADMIN_REMOVE] Admin {Hash} removed by {Sender}",
                    senderHash, data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 관리자 제거 완료!\n\nSenderHash: {senderHash}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ADMIN_REMOVE] Error processing admin remove command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "관리자 제거 중 오류가 발생했습니다."
            };
        }
    }
}
