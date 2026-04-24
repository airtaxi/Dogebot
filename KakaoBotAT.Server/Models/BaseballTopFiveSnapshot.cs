namespace KakaoBotAT.Server.Models;

public sealed record BaseballTopFiveSnapshot(
    IReadOnlyList<BaseballTopFiveStatistic> BattingTopFiveStatistics,
    IReadOnlyList<BaseballTopFiveStatistic> PitchingTopFiveStatistics);
