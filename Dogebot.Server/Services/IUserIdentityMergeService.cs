namespace Dogebot.Server.Services;

public interface IUserIdentityMergeService
{
    /// <summary>Merges room-scoped records from the old identity into the new identity.</summary>
    Task MergeRoomScopedAsync(string roomId, string oldSenderHash, string newSenderHash);

    /// <summary>Merges records that are not scoped to a room from the old identity into the new identity.</summary>
    Task MergeGlobalAsync(string oldSenderHash, string newSenderHash);

    /// <summary>Merges both room-scoped and global records from the old identity into the new identity.</summary>
    Task MergeAsync(string roomId, string oldSenderHash, string newSenderHash);
}
