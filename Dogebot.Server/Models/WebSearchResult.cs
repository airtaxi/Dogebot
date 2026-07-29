namespace Dogebot.Server.Models;

public sealed record WebSearchResult(string? Title, string? Url, string? Content, double? Score);