namespace Dogebot.Server.Models;

public sealed record BaseballNewsSnapshot(DateOnly TargetDate, IReadOnlyList<BaseballNewsItem> NewsItems);

