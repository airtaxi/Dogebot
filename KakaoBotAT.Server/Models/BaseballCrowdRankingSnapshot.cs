namespace KakaoBotAT.Server.Models;

public sealed record BaseballCrowdRankingSnapshot(string DateText, IReadOnlyList<BaseballCrowdRankingEntry> CrowdRankings);
