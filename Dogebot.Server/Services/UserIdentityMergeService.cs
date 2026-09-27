using MongoDB.Bson;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

/// <summary>
/// Merges records that reference an old sender identity into the canonical identity.
/// Room-scoped statistics are merged per room, while preferences and audit fields are merged globally.
/// </summary>
public class UserIdentityMergeService : IUserIdentityMergeService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<UserIdentityMergeService> _logger;

    public UserIdentityMergeService(IMongoDbService mongoDbService, ILogger<UserIdentityMergeService> logger)
    {
        _database = mongoDbService.Database;
        _logger = logger;
    }

    public async Task MergeAsync(string roomId, string oldSenderHash, string newSenderHash)
    {
        await MergeRoomScopedAsync(roomId, oldSenderHash, newSenderHash);
        await MergeDailyRequestCountsAsync(roomId, oldSenderHash, newSenderHash);
        await MergeGlobalAsync(oldSenderHash, newSenderHash);
    }

    public async Task MergeRoomScopedAsync(string roomId, string oldSenderHash, string newSenderHash)
    {
        if (oldSenderHash == newSenderHash) return;

        // chatStatistics: merge messageCount, lastMessageTime, senderName
        await MergeHashInCollectionAsync("chatStatistics", roomId, oldSenderHash, newSenderHash, additionalKeyFields: [], incrementFields: ["messageCount"], maxFields: ["lastMessageTime"], setFields: ["senderName"]);

        // hourlyChatStatistics: merge by dateTime
        await MergeHashInCollectionAsync("hourlyChatStatistics", roomId, oldSenderHash, newSenderHash, additionalKeyFields: ["dateTime"], incrementFields: ["messageCount"]);

        // dailyChatStatistics: merge by dayOfWeek
        await MergeHashInCollectionAsync("dailyChatStatistics", roomId, oldSenderHash, newSenderHash, additionalKeyFields: ["dayOfWeek"], incrementFields: ["messageCount"]);

        // monthlyChatStatistics: merge by month
        await MergeHashInCollectionAsync("monthlyChatStatistics", roomId, oldSenderHash, newSenderHash, additionalKeyFields: ["month"], incrementFields: ["messageCount"]);

        // roomMentionUsages: keep the latest cooldown when a sender hash changes
        await MergeHashInCollectionAsync("roomMentionUsages", roomId, oldSenderHash, newSenderHash, additionalKeyFields: [], incrementFields: [], maxFields: ["lastUsedAt", "nextAvailableAt"], setFields: ["roomName", "senderName"]);
    }

    public async Task MergeGlobalAsync(string oldSenderHash, string newSenderHash)
    {
        if (oldSenderHash == newSenderHash) return;

        // Preferences and registrations that keep a single document per sender
        await MergeUniqueSenderHashCollectionAsync("adminUsers", oldSenderHash, newSenderHash);
        await MergeUniqueSenderHashCollectionAsync("userBaseballTeamPreferences", oldSenderHash, newSenderHash);
        await MergeUniqueSenderHashCollectionAsync("userWeatherPreferences", oldSenderHash, newSenderHash);

        // Documents keyed by senderHash and a date
        await MergeHashInCollectionAsync("dailyFortuneRecords", null, oldSenderHash, newSenderHash, additionalKeyFields: ["date"], incrementFields: []);
        await MergeHashInCollectionAsync("dailyLeaveWorkRecords", null, oldSenderHash, newSenderHash, additionalKeyFields: ["date"], incrementFields: [], setOnInsertFields: ["leaveTimeMinutes"]);

        // References that only record who created or changed a document
        await UpdateIdentityFieldAsync("adminApprovalCodes", "senderHash", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("adminUsers", "addedBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("scheduledMessages", "createdBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("baseballGameSubscriptions", "createdBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("imaxNotifications", "createdBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("simSimData", "createdBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("dengAiLongReplySettings", "updatedBy", oldSenderHash, newSenderHash);
        await UpdateIdentityFieldAsync("botSettings", "updatedBy", oldSenderHash, newSenderHash);

        _logger.LogInformation("[IDENTITY_MERGE] Merged the old sender identity into the canonical identity. old={OldSenderHash}, new={NewSenderHash}", oldSenderHash, newSenderHash);
    }

    // userDailyRequests: merge daily counters so the request limit keeps counting after a client switch
    private Task MergeDailyRequestCountsAsync(string roomId, string oldSenderHash, string newSenderHash) => MergeHashInCollectionAsync("userDailyRequests", roomId, oldSenderHash, newSenderHash, additionalKeyFields: ["date"], incrementFields: ["requestCount"], maxFields: ["lastRequestTime"]);

    private async Task MergeUniqueSenderHashCollectionAsync(string collectionName, string oldSenderHash, string newSenderHash)
    {
        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var oldFilter = new BsonDocument("senderHash", oldSenderHash);
            var newFilter = new BsonDocument("senderHash", newSenderHash);

            if (await collection.Find(newFilter).Limit(1).AnyAsync())
            {
                await collection.DeleteManyAsync(oldFilter);
                return;
            }

            await collection.UpdateManyAsync(oldFilter, new BsonDocument("$set", new BsonDocument("senderHash", newSenderHash)));
        }
        catch (Exception exception) { _logger.LogWarning(exception, "[IDENTITY_MERGE] Failed to merge senderHash in {Collection}", collectionName); }
    }

    private async Task UpdateIdentityFieldAsync(string collectionName, string fieldName, string oldValue, string newValue)
    {
        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);
            var filter = new BsonDocument(fieldName, oldValue);
            var update = new BsonDocument("$set", new BsonDocument(fieldName, newValue));
            await collection.UpdateManyAsync(filter, update);
        }
        catch (Exception exception) { _logger.LogWarning(exception, "[IDENTITY_MERGE] Failed to update {Field} in {Collection}", fieldName, collectionName); }
    }

    private async Task MergeHashInCollectionAsync(string collectionName, string? roomId, string oldSenderHash, string newSenderHash, string[] additionalKeyFields, string[] incrementFields, string[]? maxFields = null, string[]? setFields = null, string[]? setOnInsertFields = null)
    {
        try
        {
            var collection = _database.GetCollection<BsonDocument>(collectionName);

            var oldFilter = new BsonDocument { { "senderHash", oldSenderHash } };
            if (roomId is not null) oldFilter.Add("roomId", roomId);

            var oldDocuments = await collection.Find(oldFilter).ToListAsync();
            if (oldDocuments.Count == 0) return;

            foreach (var oldDocument in oldDocuments)
            {
                var targetFilter = new BsonDocument { { "senderHash", newSenderHash } };
                if (roomId is not null) targetFilter.Add("roomId", roomId);
                foreach (var key in additionalKeyFields)
                    if (oldDocument.Contains(key)) targetFilter.Add(key, oldDocument[key]);

                var updateDocument = new BsonDocument();

                var incrementDocument = new BsonDocument();
                foreach (var field in incrementFields)
                    if (oldDocument.Contains(field)) incrementDocument.Add(field, oldDocument[field]);
                if (incrementDocument.ElementCount > 0) updateDocument.Add("$inc", incrementDocument);

                if (maxFields is not null)
                {
                    var maxDocument = new BsonDocument();
                    foreach (var field in maxFields)
                        if (oldDocument.Contains(field)) maxDocument.Add(field, oldDocument[field]);
                    if (maxDocument.ElementCount > 0) updateDocument.Add("$max", maxDocument);
                }

                if (setFields is not null)
                {
                    var setDocument = new BsonDocument();
                    foreach (var field in setFields)
                        if (oldDocument.Contains(field)) setDocument.Add(field, oldDocument[field]);
                    if (setDocument.ElementCount > 0) updateDocument.Add("$set", setDocument);
                }

                if (setOnInsertFields is not null)
                {
                    var setOnInsertDocument = new BsonDocument();
                    foreach (var field in setOnInsertFields)
                        if (oldDocument.Contains(field)) setOnInsertDocument.Add(field, oldDocument[field]);
                    if (setOnInsertDocument.ElementCount > 0) updateDocument.Add("$setOnInsert", setOnInsertDocument);
                }

                if (updateDocument.ElementCount == 0)
                {
                    // An upsert requires at least one operator; copy the key fields so a missing target document is created.
                    var keyDocument = new BsonDocument();
                    foreach (var key in additionalKeyFields)
                        if (oldDocument.Contains(key)) keyDocument.Add(key, oldDocument[key]);
                    if (keyDocument.ElementCount == 0) continue;
                    updateDocument.Add("$setOnInsert", keyDocument);
                }

                await collection.UpdateOneAsync(targetFilter, updateDocument, new UpdateOptions { IsUpsert = true });
            }

            await collection.DeleteManyAsync(oldFilter);
        }
        catch (Exception exception) { _logger.LogWarning(exception, "[IDENTITY_MERGE] Failed to merge senderHash in {Collection}", collectionName); }
    }
}
