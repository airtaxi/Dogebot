namespace Dogebot.Server.Services;

/// <summary>
/// Resolves sender identities for the manual identity link command and merges
/// the room-scoped records of the source identity into the target identity.
/// </summary>
public interface IUserIdentityLinkService
{
    /// <summary>
    /// Resolves a source or target argument inside the room. The argument is matched
    /// against sender hashes first and against sender names second.
    /// </summary>
    Task<SenderIdentityResolution> ResolveIdentityAsync(string roomId, string identityArgument);

    /// <summary>
    /// Merges every room-scoped record of the source identity into the target identity.
    /// </summary>
    Task LinkIdentitiesAsync(string roomId, string sourceSenderHash, string targetSenderHash);
}

/// <summary>
/// A sender identity that was resolved inside a room.
/// </summary>
public sealed record SenderIdentityReference(string SenderHash, string SenderName, long MessageCount, long LastMessageTime);

/// <summary>
/// Result of resolving an identity argument. A resolution is either a single identity,
/// an ambiguous candidate list, or an empty not-found result.
/// </summary>
public sealed record SenderIdentityResolution(SenderIdentityReference? Identity, IReadOnlyList<SenderIdentityReference> AmbiguousCandidates)
{
    public bool IsResolved => Identity is not null;
    public bool IsAmbiguous => AmbiguousCandidates.Count > 0;

    public static SenderIdentityResolution NotFound() => new(null, []);
    public static SenderIdentityResolution Resolved(SenderIdentityReference identity) => new(identity, []);
    public static SenderIdentityResolution Ambiguous(IReadOnlyList<SenderIdentityReference> ambiguousCandidates) => new(null, ambiguousCandidates);
}
