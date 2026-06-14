namespace Dogebot.Server.Services;

public interface IStockService : IDengAiCallableService
{
    Task<string> CreateSummaryMessageAsync(string queryText);
    Task<string> CreateDetailMessageAsync(string queryText);
    Task<string> CreateChartMessageAsync(string queryText);
    Task<string> CreateNewsMessageAsync(string queryText);
    Task<string> CreateMarketMessageAsync(string queryText);
}
