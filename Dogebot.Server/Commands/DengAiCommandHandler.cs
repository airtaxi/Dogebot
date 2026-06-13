using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class DengAiCommandHandler(IDengAiService dengAiService, ILogger<DengAiCommandHandler> logger) : ICommandHandler
{
    private const string DengAiCommand = "댕댕아";

    public string Command => DengAiCommand;

    public bool CanHandle(string content) => content.Trim().StartsWith(DengAiCommand, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var userMessage = ExtractUserMessage(data.Content);
            if (string.IsNullOrWhiteSpace(userMessage)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "💬 사용법: 댕댕아 (메시지)\n예시: 댕댕아 오늘 기분 어때?" };

            if (!dengAiService.IsConfigured) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "AI 설정이 아직 안 되어 있어요멍." };

            var reply = await dengAiService.GenerateReplyAsync(userMessage);

            if (string.IsNullOrWhiteSpace(reply)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "지금은 대답이 잘 안 나오고 있어요멍. 조금 뒤에 다시 불러줘요멍." };

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[DENG_AI] Responded to {SenderName} in room {RoomId}", data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = reply
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[DENG_AI] Error processing AI command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "AI 답변 중 오류가 발생했어요멍."
            };
        }
    }

    private static string ExtractUserMessage(string content)
    {
        var trimmedContent = content.Trim();
        if (trimmedContent.Length <= DengAiCommand.Length) return string.Empty;

        return trimmedContent[DengAiCommand.Length..].Trim();
    }
}
