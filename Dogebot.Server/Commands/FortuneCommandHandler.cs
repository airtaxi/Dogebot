using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !운세 command to display today's fortune.
/// </summary>
public class FortuneCommandHandler(ILogger<FortuneCommandHandler> logger, IFortuneService fortuneService) : ICommandHandler
{
    public string Command => "!운세";

    public bool CanHandle(string content) =>
        content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (await fortuneService.HasDrawnTodayAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🔮 오늘의 운세는 이미 확인하셨습니다. 내일 다시 시도해주세요!"
                };
            }

            var message = fortuneService.CreateFortuneMessage();
            if (message == null) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "❌ 운세 데이터를 불러올 수 없습니다." };

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[FORTUNE] Fortune told for {Sender} in room {RoomId}", data.SenderName, data.RoomId);

            await fortuneService.RecordDrawAsync(data.SenderHash);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[FORTUNE] Error processing fortune command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "운세를 확인하는 중 오류가 발생했습니다."
            };
        }
    }
}

