using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class DisableDengAiLongReplyCommandHandler(IBotSettingService botSettingService, IAdminService adminService, ILogger<DisableDengAiLongReplyCommandHandler> logger) : ICommandHandler
{
    public string Command => "!댕댕링크비활성화";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await adminService.IsAdminAsync(data.SenderHash)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "⛔ 권한이 없습니다. 관리자만 댕댕링크 기능을 비활성화할 수 있습니다." };

            if (!await botSettingService.IsDengAiLongReplyEnabledAsync()) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "ℹ️ 이미 댕댕링크 기능이 비활성화되어 있습니다." };

            await botSettingService.SetDengAiLongReplyEnabledAsync(false, data.SenderHash);

            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("[DENG_AI_LINK_DISABLE] Long reply link mode disabled by {Sender}", data.SenderName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 댕댕링크 기능이 비활성화되었습니다.\n\n" +
                          "이제 AI 답변이 항상 원문으로 표시됩니다."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[DENG_AI_LINK_DISABLE] Error processing disable long reply link command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "댕댕링크 기능 비활성화 중 오류가 발생했습니다."
            };
        }
    }
}