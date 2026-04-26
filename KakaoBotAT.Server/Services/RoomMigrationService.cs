using KakaoBotAT.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class RoomMigrationService : IRoomMigrationService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<RoomMigrationCode> _migrationCodes;
    private readonly IMongoCollection<RoomMigrationMapping> _migrationMappings;
    private readonly ILogger<RoomMigrationService> _logger;
    private readonly Random _random = new();

    public RoomMigrationService(IMongoDbService mongoDbService, ILogger<RoomMigrationService> logger)
    {
        _database = mongoDbService.Database;
        _migrationCodes = _database.GetCollection<RoomMigrationCode>("roomMigrationCodes");
        _migrationMappings = _database.GetCollection<RoomMigrationMapping>("roomMigrationMappings");
        _logger = logger;

        var indexKeys = Builders<RoomMigrationCode>.IndexKeys.Ascending(x => x.Code);
        var indexModel = new CreateIndexModel<RoomMigrationCode>(indexKeys);
        _migrationCodes.Indexes.CreateOne(indexModel);

        var mappingIndexKeys = Builders<RoomMigrationMapping>.IndexKeys
            .Ascending(x => x.TargetRoomId)
            .Ascending(x => x.SenderName);
        var mappingIndexModel = new CreateIndexModel<RoomMigrationMapping>(mappingIndexKeys);
        _migrationMappings.Indexes.CreateOne(mappingIndexModel);
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

        // Migrate all collections that contain roomId
        totalMigrated += await UpdateRoomIdAsync<ChatStatistics>("chatStatistics", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<MessageContent>("messageContents", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<WordContent>("wordContents", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<HourlyChatStatistics>("hourlyChatStatistics", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<DailyChatStatistics>("dailyChatStatistics", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<MonthlyChatStatistics>("monthlyChatStatistics", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<RoomRankingSettings>("roomRankingSettings", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<ScheduledMessage>("scheduledMessages", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<RoomRequestLimit>("roomRequestLimits", sourceRoomId, targetRoomId);
        totalMigrated += await UpdateRoomIdAsync<UserDailyRequest>("userDailyRequests", sourceRoomId, targetRoomId);

        // Also update roomName in settings/limits that store it
        await UpdateRoomNameAsync<RoomRankingSettings>("roomRankingSettings", targetRoomId, targetRoomName);
        await UpdateRoomNameAsync<RoomRequestLimit>("roomRequestLimits", targetRoomId, targetRoomName);

        // Record senderName→oldSenderHash mappings for lazy hash migration.
        // When a user sends a message in the target room, their old hash data
        // will be merged into their new hash.
        await RecordSenderHashMappingsAsync(targetRoomId);

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
    /// Records senderName→oldSenderHash mappings from chatStatistics for the target room.
    /// These are used for lazy senderHash migration when users send new messages.
    /// </summary>
    private async Task RecordSenderHashMappingsAsync(string targetRoomId)
    {
        try
        {
            var chatStatsCollection = _database.GetCollection<ChatStatistics>("chatStatistics");
            var users = await chatStatsCollection
                .Find(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, targetRoomId))
                .ToListAsync();

            if (users.Count == 0) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var mappings = users.Select(u => new RoomMigrationMapping
            {
                TargetRoomId = targetRoomId,
                SenderName = u.SenderName,
                OldSenderHash = u.SenderHash,
                CreatedAt = now
            }).ToList();

            await _migrationMappings.InsertManyAsync(mappings);

            _logger.LogInformation("[ROOM_MIGRATION] Recorded {Count} senderHash mappings for room {RoomId}",
                mappings.Count, targetRoomId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ROOM_MIGRATION] Failed to record senderHash mappings");
        }
    }

    public async Task<bool> TryMigrateUserHashAsync(string targetRoomId, string senderName, string newSenderHash)
    {
        var filter = Builders<RoomMigrationMapping>.Filter.And(
            Builders<RoomMigrationMapping>.Filter.Eq(x => x.TargetRoomId, targetRoomId),
            Builders<RoomMigrationMapping>.Filter.Eq(x => x.SenderName, senderName)
        );

        var mapping = await _migrationMappings.Find(filter).FirstOrDefaultAsync();
        if (mapping is null || mapping.OldSenderHash == newSenderHash)
            return false;

        // Merge old senderHash data into new senderHash across all stats collections
        await MergeUserHashAsync(targetRoomId, mapping.OldSenderHash, newSenderHash);

        // Delete the mapping (this user's migration is complete)
        await _migrationMappings.DeleteManyAsync(filter);

        _logger.LogInformation(
            "[ROOM_MIGRATION] Merged senderHash for {SenderName} in room {RoomId}",
            senderName, targetRoomId);

        return true;
    }

    /// <summary>
    /// Merges all statistics from oldSenderHash into newSenderHash for the given room.
    /// Uses $inc for counts, $max for timestamps, then deletes old documents.
    /// </summary>
    private async Task MergeUserHashAsync(string roomId, string oldSenderHash, string newSenderHash)
    {
        // chatStatistics: merge messageCount, lastMessageTime, senderName
        await MergeHashInCollectionAsync("chatStatistics", roomId, oldSenderHash, newSenderHash,
            additionalKeyFields: [],
            incrementFields: ["messageCount"],
            maxFields: ["lastMessageTime"],
            setFields: ["senderName"]);

        // hourlyChatStatistics: merge by dateTime
        await MergeHashInCollectionAsync("hourlyChatStatistics", roomId, oldSenderHash, newSenderHash,
            additionalKeyFields: ["dateTime"],
            incrementFields: ["messageCount"]);

        // dailyChatStatistics: merge by dayOfWeek
        await MergeHashInCollectionAsync("dailyChatStatistics", roomId, oldSenderHash, newSenderHash,
            additionalKeyFields: ["dayOfWeek"],
            incrementFields: ["messageCount"]);

        // monthlyChatStatistics: merge by month
        await MergeHashInCollectionAsync("monthlyChatStatistics", roomId, oldSenderHash, newSenderHash,
            additionalKeyFields: ["month"],
            incrementFields: ["messageCount"]);
    }

    /// <summary>
    /// Merges documents from oldSenderHash into newSenderHash within a single collection.
    /// </summary>
    private async Task MergeHashInCollectionAsync(
        string collectionName,
        string roomId,
        string oldSenderHash,
        string newSenderHash,
        string[] additionalKeyFields,
        string[] incrementFields,
        string[]? maxFields = null,
        string[]? setFields = null)
    {
        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var oldFilter = new BsonDocument
            {
                { "roomId", roomId },
                { "senderHash", oldSenderHash }
            };

            var oldDocs = await collection.Find(oldFilter).ToListAsync();
            if (oldDocs.Count == 0) return;

            foreach (var doc in oldDocs)
            {
                var targetFilter = new BsonDocument
                {
                    { "roomId", roomId },
                    { "senderHash", newSenderHash }
                };
                foreach (var key in additionalKeyFields)
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

            await collection.DeleteManyAsync(oldFilter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ROOM_MIGRATION] Failed to merge senderHash in {Collection}", collectionName);
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
