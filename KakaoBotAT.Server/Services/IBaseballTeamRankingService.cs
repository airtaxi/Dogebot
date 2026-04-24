using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public interface IBaseballTeamRankingService
{
    Task<BaseballTeamRankingSnapshot?> GetDailyBaseballTeamRankingSnapshotAsync();
    Task<BaseballTopFiveSnapshot?> GetBaseballTopFiveSnapshotAsync();
    Task<BaseballCrowdRankingSnapshot?> GetBaseballCrowdRankingSnapshotAsync();
    string? GetLastTeamRankingErrorDetails();
    string? GetLastPlayerTopFiveErrorDetails();
    string? GetLastCrowdRankingErrorDetails();
}
