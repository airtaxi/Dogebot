using KakaoBotAT.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class RoomMigrationService : IRoomMigrationService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<RoomMigrationCode> _migrationCodes;
    private readonly ILogger<RoomMigrationService> _logger;
    private readonly Random _random = new();

    public RoomMigrationService(IMongoDbService mongoDbService, ILogger<RoomMigrationService> logger)
    {
        _database = mongoDbService.Database;
        _migrationCodes = _database.GetCollection<RoomMigrationCode>("roomMigrationCodes");
        _logger = logger;

        var indexKeys = Builders<RoomMigrationCode>.IndexKeys.Ascending(x => x.Code);
        var indexModel = new CreateIndexModel<RoomMigrationCode>(indexKeys);
        _migrationCodes.Indexes.CreateOne(indexModel);
    }

    public async Task<string> CreateMigrationCodeAsync(string sourceRoomId, string sourceRoomName, string senderHash, string senderName)
    {
        var code = GenerateCode();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var migrationCode = new RoomMigrationCode
        {
            Code = code,
            SourceRoomId = sourceRoomId,
            SourceRoomName = sourceRoomName,
            CreatedBy = senderHash,
            CreatedByName = senderName,
            CreatedAt = now,
            ExpiresAt = now + 600 // 10 minutes
        };

        await _migrationCodes.InsertOneAsync(migrationCode);
        return code;
    }

    public async Task<RoomMigrationResult> MigrateRoomDataAsync(string code, string targetRoomId, string targetRoomName)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<RoomMigrationCode>.Filter.And(
            Builders<RoomMigrationCode>.Filter.Eq(x => x.Code, code),
            Builders<RoomMigrationCode>.Filter.Gt(x => x.ExpiresAt, now)
        );

        var migrationCode = await _migrationCodes.Find(filter).FirstOrDefaultAsync();
        if (migrationCode is null)
            return new RoomMigrationResult(false, ErrorMessage: "유효하지 않거나 만료된 코드입니다.");

        var sourceRoomId = migrationCode.SourceRoomId;

        if (sourceRoomId == targetRoomId)
            return new RoomMigrationResult(false, ErrorMessage: "원본 방과 대상 방이 동일합니다.");

        var totalMigrated = 0;

        // Merge count-based collections (upsert with $inc to avoid duplicate key conflicts)
        totalMigrated += await MergeRoomDocumentsAsync("chatStatistics", sourceRoomId, targetRoomId,
            ["senderHash"], ["messageCount"], maxFields: ["lastMessageTime"], setFields: ["senderName"]);
        totalMigrated += await MergeRoomDocumentsAsync("messageContents", sourceRoomId, targetRoomId,
            ["content"], ["count"], maxFields: ["lastTime"]);
        totalMigrated += await MergeRoomDocumentsAsync("wordContents", sourceRoomId, targetRoomId,
            ["word"], ["count"], maxFields: ["lastTime"]);
        totalMigrated += await MergeRoomDocumentsAsync("hourlyChatStatistics", sourceRoomId, targetRoomId,
            ["senderHash", "dateTime"], ["messageCount"]);
        totalMigrated += await MergeRoomDocumentsAsync("dailyChatStatistics", sourceRoomId, targetRoomId,
            ["senderHash", "dayOfWeek"], ["messageCount"]);
        totalMigrated += await MergeRoomDocumentsAsync("monthlyChatStatistics", sourceRoomId, targetRoomId,
            ["senderHash", "month"], ["messageCount"]);

        // Simple roomId update for non-count collections
        totalMigrated += await UpdateRoomIdAsync<RoomRankingSettings>("roomRankingSettings", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<ScheduledMessage>("scheduledMessages", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<RoomRequestLimit>("roomRequestLimits", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<UserDailyRequest>("userDailyRequests", sourceRoomId, targetRoomId);

        // Also update roomName in settings/limits that store it
        await UpdateRoomNameAsync<RoomRankingSettings>("roomRankingSettings", targetRoomId, targetRoomName);
        await UpdateRoomNameAsync<RoomRequestLimit>("roomRequestLimits", targetRoomId, targetRoomName);

        // Delete the used migration code
        await _migrationCodes.DeleteOneAsync(Builders<RoomMigrationCode>.Filter.Eq(x => x.Id, migrationCode.Id));

        _logger.LogWarning("[ROOM_MIGRATION] Migrated {Count} documents from room {SourceRoom} to room {TargetRoom}",
            totalMigrated, migrationCode.SourceRoomName, targetRoomName);

        return new RoomMigrationResult(true, SourceRoomName: migrationCode.SourceRoomName, TotalDocumentsMigrated: totalMigrated);
    }

    public async Task<int> DeleteExpiredMigrationCodesAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<RoomMigrationCode>.Filter.Lte(x => x.ExpiresAt, now);
        var result = await _migrationCodes.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }

    /// <summary>
    /// Merges documents from source room into target room by upserting with $inc for count fields.
    /// Avoids duplicate key conflicts on collections with unique compound indexes.
    /// </summary>
    private async Task<int> MergeRoomDocumentsAsync(
        string collectionName,
        string sourceRoomId,
        string targetRoomId,
        string[] uniqueKeyFields,
        string[] incrementFields,
        string[]? maxFields = null,
        string[]? setFields = null)
    {
        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var sourceFilter = new BsonDocument("roomId", sourceRoomId);
            var sourceDocs = await collection.Find(sourceFilter).ToListAsync();

            if (sourceDocs.Count == 0) return 0;

            foreach (var doc in sourceDocs)
            {
                var targetFilter = new BsonDocument("roomId", targetRoomId);
                foreach (var key in uniqueKeyFields)
                    targetFilter.Add(key, doc[key]);

                var updateDoc = new BsonDocument();

                var incDoc = new BsonDocument();
                foreach (var field in incrementFields)
                    if (doc.Contains(field))
                        incDoc.Add(field, doc[field]);
                if (incDoc.ElementCount > 0)
                    updateDoc.Add("$inc", incDoc);

                if (maxFields is not null)
                {
                    var maxDoc = new BsonDocument();
                    foreach (var field in maxFields)
                        if (doc.Contains(field))
                            maxDoc.Add(field, doc[field]);
                    if (maxDoc.ElementCount > 0)
                        updateDoc.Add("$max", maxDoc);
                }

                if (setFields is not null)
                {
                    var setDoc = new BsonDocument();
                    foreach (var field in setFields)
                        if (doc.Contains(field))
                            setDoc.Add(field, doc[field]);
                    if (setDoc.ElementCount > 0)
                        updateDoc.Add("$set", setDoc);
                }

                await collection.UpdateOneAsync(targetFilter, updateDoc, new UpdateOptions { IsUpsert = true });
            }

            await collection.DeleteManyAsync(sourceFilter);

            _logger.LogInformation("[ROOM_MIGRATION] {Collection}: {Count} documents merged", collectionName, sourceDocs.Count);
            return sourceDocs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ROOM_MIGRATION] {Collection}: Error during merge migration", collectionName);
            return 0;
        }
    }

    /// <summary>
    /// Updates roomId field for all documents matching the source roomId in the given collection.
    /// </summary>
    private async Task<int> UpdateRoomIdAsync<T>(string collectionName, string sourceRoomId, string targetRoomId)
    {
        try
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Eq("roomId", sourceRoomId);
            var update = Builders<T>.Update.Set("roomId", targetRoomId);
            var result = await collection.UpdateManyAsync(filter, update);
            var count = (int)result.ModifiedCount;

            if (count > 0)
                _logger.LogInformation("[ROOM_MIGRATION] {Collection}: {Count} documents migrated", collectionName, count);

            return count;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning("[ROOM_MIGRATION] {Collection}: Duplicate key conflict during migration, skipping", collectionName);
            return 0;
        }
    }

    /// <summary>
    /// Updates roomName field for documents with the given roomId.
    /// </summary>
    private async Task UpdateRoomNameAsync<T>(string collectionName, string roomId, string roomName)
    {
        try
        {
            var collection = _database.GetCollection<T>(collectionName);
            var filter = Builders<T>.Filter.Eq("roomId", roomId);
            var update = Builders<T>.Update.Set("roomName", roomName);
            await collection.UpdateManyAsync(filter, update);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ROOM_MIGRATION] Failed to update roomName in {Collection}", collectionName);
        }
    }

    private string GenerateCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
