namespace Dogebot.Server.Services;

public interface IUserIdentityMergeService
{
    /// <summary>Merges room-scoped records from the old identity into the new identity.</summary>
    Task MergeRoomScopedAsync(string roomId, string oldSenderHash, string newSenderHash);
}
