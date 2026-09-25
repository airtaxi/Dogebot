using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballTeamPreferenceRemoveCommandHandler(IUserBaseballTeamPreferenceService userBaseballTeamPreferenceService, ILogger<BaseballTeamPreferenceRemoveCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구팀등록해제";

    public bool CanHandle(string content) => content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var removedTeamName = await userBaseballTeamPreferenceService.RemoveUserPreferredTeamAsync(data.SenderHash);

            if (removedTeamName is null) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "등록된 응원팀이 없습니다.\n등록하려면 !야구팀등록 (팀명)을 입력해주세요. 예시: !야구팀등록 KIA" };

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[BASEBALL_TEAM_PREFERENCE] {Sender} removed preferred team {TeamName} in room {RoomId}", data.SenderName, removedTeamName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 응원팀 등록을 해제했습니다. (해제한 팀: {removedTeamName})\n다시 등록하려면 !야구팀등록 (팀명)을 입력해주세요."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_TEAM_PREFERENCE] Error processing team preference removal command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "야구 팀 등록 해제 중 오류가 발생했습니다."
            };
        }
    }
}
