namespace Dogebot.Server.Services;

public interface IDengAiLongReplyService
{
    bool IsBaseUrlConfigured { get; }

    Task<string?> StoreAndGetUrlAsync(string content);

    Task<string?> GetContentByUrlHashAsync(string urlHash);

    string? GetReplyUrl(string urlHash);

    Task<bool> IsEnabledAsync(string roomId);

    Task SetEnabledAsync(string roomId, string roomName, bool enabled, string updatedBy);
}