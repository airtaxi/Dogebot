namespace Dogebot.Server.Services;

public interface ILeaveWorkService
{
    Task<string> CreateLeaveWorkMessageAsync(string senderHash, CancellationToken cancellationToken = default);
}
