using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class AdminAddCommandHandler : ICommandHandler
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminAddCommandHandler> _logger;

    public AdminAddCommandHandler(
        IAdminService adminService,
        ILogger<AdminAddCommandHandler> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!관리추가";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                var approvalCode = await _adminService.CreateApprovalCodeAsync(
                    data.SenderHash,
                    data.SenderName,
                    data.RoomId,
                    data.RoomName);

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[ADMIN_ADD] {Sender} requested admin approval code: {Code}",
                        data.SenderName, approvalCode);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"🔐 관리자 승인 요청\n\n" +
                             $"승인 코드: {approvalCode}\n\n" +
                             $"⏰ 10분 이내에 최고 관리자의 개인톡에서\n" +
                             $"!관리추가 {approvalCode}\n" +
                             $"를 입력하여 승인받으세요.\n\n" +
                             $"⚠️ RoomId마다 SenderHash가 다르므로,\n" +
                             $"반드시 개인톡에서 입력해주세요!"
                };
            }

            if (parts.Length == 2)
            {
                if (data.SenderHash != _adminService.ChiefAdminHash)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "⛔ 권한이 없습니다. 최고 관리자만 승인할 수 있습니다."
                    };
                }

                var code = parts[1];
                var approved = await _adminService.ApproveAdminAsync(code, data.SenderHash);

                if (!approved)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("[ADMIN_ADD] Failed to approve code {Code} by {Sender}",
                            code, data.SenderName);

                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "❌ 승인 실패\n\n" +
                                 "• 유효하지 않은 코드이거나\n" +
                                 "• 승인 시간이 만료되었거나\n" +
                                 "• 이미 관리자인 사용자입니다."
                    };
                }

                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[ADMIN_ADD] Code {Code} approved by {Sender}",
                        code, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"✅ 관리자 승인 완료!\n\n" +
                             $"승인 코드: {code}\n" +
                             $"이제 해당 사용자는 관리자 기능을 사용할 수 있습니다."
                };
            }

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "🔐 사용법:\n\n" +
                         "1️⃣ 관리자가 되려는 사람:\n" +
                         "   !관리추가\n\n" +
                         "2️⃣ 최고 관리자 (승인):\n" +
                         "   !관리추가 (승인코드)\n\n" +
                         "⚠️ 승인은 반드시 개인톡에서 해주세요!"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ADMIN_ADD] Error processing admin add command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "관리자 추가 중 오류가 발생했습니다."
            };
        }
    }
}
