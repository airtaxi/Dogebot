using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IBaseballGameScheduleService
{
    Task<BaseballGameScheduleSnapshot?> GetTodayGameSnapshotAsync();
    Task<BaseballGameDetail?> GetTodayGameDetailAsync(long gameId);
    Task<BaseballGameScheduleSnapshot?> GetTomorrowGameSnapshotAsync();
    Task<BaseballGameDetail?> GetTomorrowGameDetailAsync(long gameId);
    string? GetLastGameScheduleErrorDetails();
}
