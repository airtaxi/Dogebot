namespace KakaoBotAT.Server.Models;

public sealed record BaseballNewsSnapshot(DateOnly TargetDate, IReadOnlyList<BaseballNewsItem> NewsItems);
