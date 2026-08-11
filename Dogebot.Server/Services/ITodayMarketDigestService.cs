namespace Dogebot.Server.Services;

public interface ITodayMarketDigestService : IDengAiCallableService
{
    Task<string?> GetTodayMarketDigestAsync();
    DateTime? GetLastCacheTime();
}
