namespace Dogebot.Server.Services;

public interface IUnitConversionService : IDengAiCallableService
{
    Task<string> CreateUnitConversionMessageAsync(string queryText);
}
