using Dogebot.Commons;

namespace Dogebot.Server.Services;

public interface IIdentityResolutionService
{
    /// <summary>
    /// Resolves the canonical sender identity for the message and merges legacy sender hashes
    /// when a mobile notification hash and a LOCO account id are linked for the first time.
    /// </summary>
    Task<string> ResolveSenderHashAsync(KakaoMessageData data);
}
