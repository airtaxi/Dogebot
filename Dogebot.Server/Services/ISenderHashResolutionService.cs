using Dogebot.Commons;

namespace Dogebot.Server.Services;

public interface ISenderHashResolutionService
{
    /// <summary>
    /// Resolves the sender hash for an incoming message. A completely new hash in the room
    /// is migrated to the hash of the same-name sender document when one exists.
    /// </summary>
    Task<string> ResolveSenderHashAsync(KakaoMessageData data);
}
