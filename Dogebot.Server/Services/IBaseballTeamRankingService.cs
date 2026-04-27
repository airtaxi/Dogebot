using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IBaseballTeamRankingService
{
    Task<BaseballTeamRankingSnapshot?> GetDailyBaseballTeamRankingSnapshotAsync();
    Task<BaseballTopFiveSnapshot?> GetBaseballTopFiveSnapshotAsync();
    Task<BaseballCrowdRankingSnapshot?> GetBaseballCrowdRankingSnapshotAsync();
    Task<BaseballNewsSnapshot?> GetBaseballNewsSnapshotAsync();
    string? GetLastTeamRankingErrorDetails();
    string? GetLastPlayerTopFiveErrorDetails();
    string? GetLastCrowdRankingErrorDetails();
    string? GetLastNewsErrorDetails();
}

