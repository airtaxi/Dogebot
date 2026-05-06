using Dogebot.Commons;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class SingleMessageCommandHandler(
    IBotSettingService botSettingService,
    IAdminService adminService,
    ILogger<SingleMessageCommandHandler> logger) : ICommandHandler
{
    public string Command => "!싱글메시지";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (data.SenderHash != adminService.ChiefAdminHash)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 최고 관리자만 메시지 전송 모드를 변경할 수 있습니다."
                };
            }

            await botSettingService.SetMessageDeliveryModeAsync(MessageDeliveryMode.Single, data.SenderHash);

            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("[SINGLE_MESSAGE] Message delivery mode changed to Single by {Sender}", data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 싱글메시지 모드가 활성화되었습니다.\n\n" +
                          "이제 pending 메시지를 기존처럼 한 번에 하나씩 전송합니다."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[SINGLE_MESSAGE] Error processing single message command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "싱글메시지 모드 변경 중 오류가 발생했습니다."
            };
        }
    }
}
