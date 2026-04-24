using System.Text;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Baseball;
using KakaoBotAT.Server.Models;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class BaseballTeamRankingCommandHandler(
    IBaseballTeamRankingService baseballTeamRankingService,
    ILogger<BaseballTeamRankingCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구팀순위";

    public bool CanHandle(string content) => content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var content = data.Content.Trim();
            var teamSearchText = content.Length > Command.Length ? content[Command.Length..].Trim() : string.Empty;
            var rankingSnapshot = await baseballTeamRankingService.GetDailyBaseballTeamRankingSnapshotAsync();

            if (rankingSnapshot == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "야구 팀 순위를 가져오지 못했습니다."
                };
            }

            if (string.IsNullOrWhiteSpace(teamSearchText))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = FormatAllTeamStandingsMessage(rankingSnapshot)
                };
            }

            var matchedTeamStandings = FindMatchingTeamStandings(rankingSnapshot.TeamStandings, teamSearchText);
            if (matchedTeamStandings.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"'{teamSearchText}'에 해당하는 팀을 찾지 못했습니다.\n예시: !야구팀순위 KT, !야구팀순위 LG, !야구팀순위 쥐"
                };
            }

            if (matchedTeamStandings.Count > 1)
            {
                var matchedTeamNames = string.Join(", ", matchedTeamStandings.Select(teamStanding => teamStanding.TeamName));
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"'{teamSearchText}' 검색 결과가 여러 팀과 일치합니다: {matchedTeamNames}\n더 구체적으로 입력해주세요."
                };
            }

            var matchedTeamStanding = matchedTeamStandings[0];

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_RANKING] Baseball team ranking requested by {Sender} in room {RoomId} for {TeamName}",
                    data.SenderName, data.RoomId, matchedTeamStanding.TeamName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatSingleTeamStandingMessage(rankingSnapshot, matchedTeamStanding)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_RANKING] Error processing baseball team ranking command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "야구 팀 순위 조회 중 오류가 발생했습니다."
            };
        }
    }

    private static List<BaseballTeamStanding> FindMatchingTeamStandings(IReadOnlyList<BaseballTeamStanding> teamStandings, string teamSearchText)
    {
        var normalizedTeamSearchText = NormalizeTeamSearchText(teamSearchText);

        return [.. teamStandings.Where(teamStanding =>
            BaseballTeamAliasCatalog.GetSearchAliases(teamStanding.TeamName)
                .Select(NormalizeTeamSearchText)
                .Any(searchAlias =>
                    searchAlias.Contains(normalizedTeamSearchText, StringComparison.OrdinalIgnoreCase) ||
                    normalizedTeamSearchText.Contains(searchAlias, StringComparison.OrdinalIgnoreCase)))];
    }

    private static string FormatAllTeamStandingsMessage(BaseballTeamRankingSnapshot rankingSnapshot)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ KBO 팀 순위 ({rankingSnapshot.RankingDate:yyyy-MM-dd} 기준)");
        stringBuilder.AppendLine();

        foreach (var teamStanding in rankingSnapshot.TeamStandings.OrderBy(teamStanding => teamStanding.Rank))
            stringBuilder.AppendLine($"{teamStanding.Rank}위 {teamStanding.TeamName} - {teamStanding.Games}경기 {teamStanding.Wins}승 {teamStanding.Losses}패 {teamStanding.Draws}무, 승률 {teamStanding.WinningPercentage:0.000}, 게임차 {teamStanding.GamesBehind}");

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatSingleTeamStandingMessage(BaseballTeamRankingSnapshot rankingSnapshot, BaseballTeamStanding teamStanding)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ {teamStanding.TeamName} 팀 순위 ({rankingSnapshot.RankingDate:yyyy-MM-dd} 기준)");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"팀명: {teamStanding.TeamName}");
        stringBuilder.AppendLine($"순위: {teamStanding.Rank}위");
        stringBuilder.AppendLine($"경기: {teamStanding.Games}경기");
        stringBuilder.AppendLine($"전적: {teamStanding.Wins}승 {teamStanding.Losses}패 {teamStanding.Draws}무");
        stringBuilder.AppendLine($"승률: {teamStanding.WinningPercentage:0.000}");
        stringBuilder.AppendLine($"게임차: {teamStanding.GamesBehind}");
        stringBuilder.AppendLine($"최근 10경기: {FormatThreePartRecord(teamStanding.RecentTenGames)}");
        stringBuilder.AppendLine($"연속: {teamStanding.Streak}");
        stringBuilder.AppendLine($"홈: {FormatThreePartRecord(teamStanding.HomeRecord)}");
        stringBuilder.AppendLine($"방문: {FormatThreePartRecord(teamStanding.AwayRecord)}");
        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatThreePartRecord(string recordText)
    {
        var separatedParts = recordText.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return separatedParts.Length == 3 ? $"{separatedParts[0]}승 {separatedParts[1]}무 {separatedParts[2]}패" : recordText;
    }

    private static string NormalizeTeamSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}
