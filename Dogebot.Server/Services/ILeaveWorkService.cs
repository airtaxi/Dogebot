namespace Dogebot.Server.Services;

public interface ILeaveWorkService
{
    Task<string> CreateLeaveWorkMessageAsync(CancellationToken cancellationToken = default);
}
