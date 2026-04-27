namespace Dogebot.Server.Models;

public sealed record BaseballTopFiveStatistic(string StatisticName, IReadOnlyList<BaseballPlayerTopFiveEntry> PlayerEntries);

