namespace KakaoBotAT.Server.Services;

public interface IRoomMigrationService
{
    /// <summary>
    /// Creates a migration code for the given room. Valid for 10 minutes.
    /// </summary>
    Task<string> CreateMigrationCodeAsync(string sourceRoomId, string sourceRoomName, string senderHash, string senderName);

    /// <summary>
    /// Migrates all room data from the source room (identified by code) to the target room.
    /// Returns a summary of migrated collections.
    /// </summary>
    Task<RoomMigrationResult> MigrateRoomDataAsync(string code, string targetRoomId, string targetRoomName);

    /// <summary>
    /// Deletes expired migration codes.
    /// </summary>
    Task<int> DeleteExpiredMigrationCodesAsync();

    /// <summary>
    /// Attempts to migrate a user's old senderHash data to their new senderHash
    /// when they send a message in a migrated room. Matches by senderName.
    /// Returns true if a mapping was found and merged.
    /// </summary>
    Task<bool> TryMigrateUserHashAsync(string targetRoomId, string senderName, string newSenderHash);
}

public record RoomMigrationResult(
    bool Success,
    string? ErrorMessage = null,
    string? SourceRoomName = null,
    int TotalDocumentsMigrated = 0);
