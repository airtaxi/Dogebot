using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IHanRiverWaterQualityService : IDengAiCallableService
{
    Task<IReadOnlyList<WposWaterQualityRow>?> GetLatestWaterQualityAsync(CancellationToken cancellationToken = default);
}