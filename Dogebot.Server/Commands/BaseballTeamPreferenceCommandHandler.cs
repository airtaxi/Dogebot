using Dogebot.Commons;
using Dogebot.Server.Baseball;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class BaseballTeamPreferenceCommandHandler(IUserBaseballTeamPreferenceService userBaseballTeamPreferenceService, ILogger<BaseballTeamPreferenceCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구팀등록";

    public bool CanHandle(string content) => content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var content = data.Content.Trim();
            var teamSearchText = content.Length > Command.Length ? content[Command.Length..].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(teamSearchText))
            {
                var registeredTeam = await userBaseballTeamPreferenceService.GetUserPreferredTeamAsync(data.SenderHash);
                var message = registeredTeam is null ? $"사용법: {Command} (팀명)\n예시: {Command} KIA, {Command} LG, {Command} 쥐" : $"현재 등록된 응원팀: {registeredTeam}\n변경하려면 팀명을 입력해주세요. 예시: {Command} KIA";

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = message
                };
            }

            var matchedTeamName = ResolveOfficialTeamName(teamSearchText);
            if (matchedTeamName is null) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"'{teamSearchText}'에 해당하는 KBO 팀을 찾지 못했습니다.\n사용 가능한 팀: LG, 롯데, 두산, 삼성, NC, KT, SSG, KIA, 한화, 키움"};

            await userBaseballTeamPreferenceService.SetUserPreferredTeamAsync(data.SenderHash, matchedTeamName);

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[BASEBALL_TEAM_PREFERENCE] {Sender} registered team {TeamName} in room {RoomId}", data.SenderName, matchedTeamName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 응원팀을 {matchedTeamName}(으)로 등록했습니다.\n이후 !오늘야구, !야구팀순위, !야구구독 등에서 팀명을 생략하면 자동으로 사용됩니다."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_TEAM_PREFERENCE] Error processing team registration command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "야구 팀 등록 중 오류가 발생했습니다."
            };
        }
    }

    private static string? ResolveOfficialTeamName(string teamSearchText)
    {
        var normalizedTeamSearchText = NormalizeTeamSearchText(teamSearchText);

        foreach (var teamAliasDefinition in BaseballTeamAliasCatalog.TeamAliasDefinitions)
        {
            var hasExactMatch = teamAliasDefinition.SearchAliases
                .Select(NormalizeTeamSearchText)
                .Any(searchAlias => searchAlias.Equals(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase));
            if (hasExactMatch) return teamAliasDefinition.OfficialTeamName;
        }

        return null;
    }

    private static string NormalizeTeamSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}
