namespace Dogebot.Server.Models;

public sealed record BaseballTeamRankingSnapshot(DateOnly RankingDate, IReadOnlyList<BaseballTeamStanding> TeamStandings);

