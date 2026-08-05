namespace Dogebot.Server.Services;

public interface ILeaveWorkService
{
    Task<bool> HasDrawnTodayAsync(string senderHash);
    Task RecordDrawAsync(string senderHash);
    Task<string> CreateLeaveWorkMessageAsync(CancellationToken cancellationToken = default);
}
