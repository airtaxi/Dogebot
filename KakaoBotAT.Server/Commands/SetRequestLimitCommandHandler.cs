using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class SetRequestLimitCommandHandler : ICommandHandler
{
    private readonly IRequestLimitService _requestLimitService;
    private readonly IAdminService _adminService;
    private readonly ILogger<SetRequestLimitCommandHandler> _logger;

    public SetRequestLimitCommandHandler(
        IRequestLimitService requestLimitService,
        IAdminService adminService,
        ILogger<SetRequestLimitCommandHandler> logger)
    {
        _requestLimitService = requestLimitService;
        _adminService = adminService;
        _logger = logger;
    }

    public string Command => "!제한설정";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
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
                    Message = "⛔ 권한이 없습니다. 관리자만 제한을 설정할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⚙️ 사용법:\n\n" +
                             "!제한설정 (횟수)\n" +
                             "예: !제한설정 10\n\n" +
                             "💡 제한이 설정되면 관리자가 아닌 사용자는\n" +
                             "   하루에 설정된 횟수만큼만 요청할 수 있습니다."
                };
            }

            if (parts.Length == 2)
            {
                var param = parts[1];

                if (!int.TryParse(param, out int dailyLimit))
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "❌ 제한 횟수는 숫자여야 합니다.\n\n사용법: !제한설정 (횟수)"
                    };
                }

                if (dailyLimit <= 0)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "❌ 제한 횟수는 1 이상이어야 합니다."
                    };
                }

                var success = await _requestLimitService.SetLimitAsync(
                    data.RoomId,
                    data.RoomName,
                    dailyLimit,
                    data.SenderHash);

                if (!success)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                        _logger.LogError("[REQUEST_LIMIT_SET] Failed to set limit {Limit} for room {RoomName} by {Sender}",
                            dailyLimit, data.RoomName, data.SenderName);

                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "❌ 제한 설정에 실패했습니다."
                    };
                }

                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[REQUEST_LIMIT_SET] Limit set to {Limit} for room {RoomName} by {Sender}",
                        dailyLimit, data.RoomName, data.SenderName);

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"✅ 요청 제한 설정 완료!\n\n" +
                             $"제한 횟수: {dailyLimit}회/일\n\n" +
                             $"이제 이 방에서는 관리자가 아닌 사용자의\n" +
                             $"하루 요청 횟수가 제한됩니다.\n\n" +
                             $"💡 관리자는 제한에서 제외됩니다."
                };
            }

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "⚙️ 사용법:\n!제한설정 (횟수)\n\n예: !제한설정 10"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REQUEST_LIMIT_SET] Error processing set request limit command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "요청 제한 설정 중 오류가 발생했습니다."
            };
        }
    }
}
