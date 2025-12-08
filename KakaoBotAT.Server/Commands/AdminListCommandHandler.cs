using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;
using System.Text;

namespace KakaoBotAT.Server.Commands;

public class AdminListCommandHandler : ICommandHandler
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminListCommandHandler> _logger;

    public AdminListCommandHandler(
        IAdminService adminService,
        ILogger<AdminListCommandHandler> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!관리목록";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await _adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 관리자만 조회할 수 있습니다."
                };
            }

            var admins = await _adminService.GetAdminListAsync();

            if (admins.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "👮 등록된 관리자가 없습니다.\n\n" +
                             "(최고 관리자는 목록에 표시되지 않습니다)"
                };
            }

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("👮 관리자 목록");
            messageBuilder.AppendLine();

            string? currentRoom = null;
            int totalCount = 0;

            foreach (var (roomName, senderName, senderHash, addedAt) in admins)
            {
                if (currentRoom != roomName)
                {
                    if (currentRoom != null)
                        messageBuilder.AppendLine();

                    currentRoom = roomName;
                    messageBuilder.AppendLine($"📍 {roomName}");
                }

                var addedDate = DateTimeOffset.FromUnixTimeSeconds(addedAt).ToLocalTime();
                messageBuilder.AppendLine($"• {senderName}");
                messageBuilder.AppendLine($"  Hash: {senderHash[..16]}...");
                messageBuilder.AppendLine($"  등록일: {addedDate:yyyy-MM-dd HH:mm}");
                
                totalCount++;
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("━━━━━━━━━━━━━━━━━━");
            messageBuilder.AppendLine($"총 {totalCount}명의 관리자");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("⚠️ 최고 관리자는 목록에 표시되지 않습니다.");

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[ADMIN_LIST] Admin {Sender} viewed admin list ({Count} admins)",
                    data.SenderName, totalCount);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = messageBuilder.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ADMIN_LIST] Error processing admin list command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "관리자 목록 조회 중 오류가 발생했습니다."
            };
        }
    }
}
