using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public interface IBaseballTeamRankingService
{
    Task<BaseballTeamRankingSnapshot?> GetDailyBaseballTeamRankingSnapshotAsync();
}
