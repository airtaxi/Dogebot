namespace Dogebot.Server.Services;

public interface IFortuneService : IDengAiCallableService
{
    Task<bool> HasDrawnTodayAsync(string senderHash);
    Task RecordDrawAsync(string senderHash);
    string? CreateFortuneMessage();
}

