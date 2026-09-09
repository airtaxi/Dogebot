using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class EnableDengAiLongReplyCommandHandler(IBotSettingService botSettingService, IDengAiLongReplyService dengAiLongReplyService, IAdminService adminService, ILogger<EnableDengAiLongReplyCommandHandler> logger) : ICommandHandler
{
    public string Command => "!댕댕링크활성화";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await adminService.IsAdminAsync(data.SenderHash)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "⛔ 권한이 없습니다. 관리자만 댕댕링크 기능을 활성화할 수 있습니다." };

            if (await botSettingService.IsDengAiLongReplyEnabledAsync()) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "ℹ️ 이미 댕댕링크 기능이 활성화되어 있습니다." };

            await botSettingService.SetDengAiLongReplyEnabledAsync(true, data.SenderHash);

            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("[DENG_AI_LINK_ENABLE] Long reply link mode enabled by {Sender}", data.SenderName);

            var baseUrlWarning = dengAiLongReplyService.IsBaseUrlConfigured ? string.Empty : "\n\n⚠️ DOGEBOT_PUBLIC_BASE_URL 환경변수가 설정되지 않아 링크가 생성되지 않습니다.";

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "✅ 댕댕링크 기능이 활성화되었습니다.\n\n" +
                          "이제 200자 이상의 AI 답변은 링크로 표시됩니다." +
                          baseUrlWarning
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[DENG_AI_LINK_ENABLE] Error processing enable long reply link command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "댕댕링크 기능 활성화 중 오류가 발생했습니다."
            };
        }
    }
}