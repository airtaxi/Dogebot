using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IBaseballGameScheduleService
{
    Task<BaseballGameScheduleSnapshot?> GetTodayGameSnapshotAsync();
    Task<BaseballGameDetail?> GetTodayGameDetailAsync(long gameId);
    Task<BaseballGameScheduleSnapshot?> GetTomorrowGameSnapshotAsync();
    Task<BaseballGameDetail?> GetTomorrowGameDetailAsync(long gameId);
    Task<BaseballGameScheduleSnapshot?> GetGameSnapshotAsync(DateOnly targetDate);
    Task<BaseballGameDetail?> GetGameDetailAsync(DateOnly targetDate, long gameId);
    string? GetLastGameScheduleErrorDetails();
}
