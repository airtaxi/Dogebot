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

            var matchedTeamNames = FindMatchingTeamNames(teamSearchText);
            if (matchedTeamNames.Count == 0) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"'{teamSearchText}'에 해당하는 KBO 팀을 찾지 못했습니다.\n사용 가능한 팀: LG, 롯데, 두산, 삼성, NC, KT, SSG, KIA, 한화, 키움"};

            if (matchedTeamNames.Count > 1)
            {
                var matchedTeamNameList = string.Join(", ", matchedTeamNames);
                return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"'{teamSearchText}' 검색 결과가 여러 팀과 일치합니다: {matchedTeamNameList}\n더 구체적으로 입력해주세요."};
            }

            var matchedTeamName = matchedTeamNames[0];

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

    private static List<string> FindMatchingTeamNames(string teamSearchText)
    {
        var normalizedTeamSearchText = NormalizeTeamSearchText(teamSearchText);
        var teamMatches = BaseballTeamAliasCatalog.TeamAliasDefinitions
            .Select(teamAliasDefinition =>
            {
                var normalizedSearchAliases = teamAliasDefinition.SearchAliases
                    .Select(NormalizeTeamSearchText)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new
                {
                    OfficialTeamName = teamAliasDefinition.OfficialTeamName,
                    HasExactMatch = normalizedSearchAliases.Any(searchAlias => searchAlias.Equals(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase)),
                    HasPartialMatch = normalizedSearchAliases.Any(searchAlias => searchAlias.Contains(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase) || normalizedTeamSearchText.Contains(searchAlias, StringComparison.OrdinalIgnoreCase))
                };
            })
            .Where(teamMatch => teamMatch.HasPartialMatch)
            .ToList();

        var exactMatchedTeamNames = teamMatches
            .Where(teamMatch => teamMatch.HasExactMatch)
            .Select(teamMatch => teamMatch.OfficialTeamName)
            .ToList();
        if (exactMatchedTeamNames.Count > 0) return exactMatchedTeamNames;

        return [.. teamMatches.Select(teamMatch => teamMatch.OfficialTeamName)];
    }

    private static string NormalizeTeamSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}
