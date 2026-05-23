namespace Dogebot.Server.Services;

public interface IExchangeRateService
{
    Task<string> CreateExchangeRateMessageAsync(string queryText);
}
