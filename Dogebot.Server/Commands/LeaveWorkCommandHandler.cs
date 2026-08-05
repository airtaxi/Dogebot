using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !퇴근 command.
/// Intentionally hidden from the help message.
/// </summary>
public class LeaveWorkCommandHandler(ILogger<LeaveWorkCommandHandler> logger, ILeaveWorkService leaveWorkService) : ICommandHandler
{
    public string Command => "!퇴근";

    public bool CanHandle(string content) =>
        content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (await leaveWorkService.HasDrawnTodayAsync(data.SenderHash)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "⏰ 오늘의 퇴근 시간은 이미 확인하셨습니다. 내일 다시 시도해주세요!" };

            var message = await leaveWorkService.CreateLeaveWorkMessageAsync();

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[LEAVE_WORK] Leave work message sent to {Sender} in room {RoomId}", data.SenderName, data.RoomId);

            await leaveWorkService.RecordDrawAsync(data.SenderHash);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[LEAVE_WORK] Error processing leave work command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "퇴근 시간 확인 중 오류가 발생했습니다."
            };
        }
    }
}
