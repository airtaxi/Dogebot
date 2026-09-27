using MongoDB.Bson;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

/// <summary>
/// Merges room-scoped records that reference an old sender identity into the new identity.
/// Used when a room migration changes the sender hashes of the room members.
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
