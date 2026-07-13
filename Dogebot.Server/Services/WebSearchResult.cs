namespace Dogebot.Server.Services;

public sealed record WebSearchResult(string? Title, string? Url, string? Content, double? Score);