namespace Dogebot.Server.Services;

public interface IExchangeRateService : IDengAiCallableService
{
    Task<string> CreateExchangeRateMessageAsync(string queryText);
}
