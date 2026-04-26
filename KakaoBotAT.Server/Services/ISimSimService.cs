namespace KakaoBotAT.Server.Services;

public interface ISimSimService
{
    Task AddResponseAsync(string message, string response, string createdBy);
    Task<bool> DeleteResponseAsync(string message, string response);
    Task<long> DeleteAllResponsesForMessageAsync(string message);
    Task<List<string>> GetResponsesAsync(string message);
    Task<int> GetResponseCountAsync(string message);
    Task<List<(string Message, int Count)>> GetTopMessagesAsync(int limit = 10);
}
