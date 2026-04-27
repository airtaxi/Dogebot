namespace Dogebot.Server.Services;

public interface IFortuneService
{
    Task<bool> HasDrawnTodayAsync(string senderHash);
    Task RecordDrawAsync(string senderHash);
}

