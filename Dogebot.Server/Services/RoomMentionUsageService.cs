using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class RoomMentionUsageService : IRoomMentionUsageService
{
    private const long MentionCooldownSeconds = 24 * 60 * 60;

    private readonly IMongoCollection<RoomMentionUsage> _roomMentionUsages;

    public RoomMentionUsageService(IMongoDbService mongoDbService)
    {
        _roomMentionUsages = mongoDbService.Database.GetCollection<RoomMentionUsage>("roomMentionUsages");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var roomMentionUsageIndexKeys = Builders<RoomMentionUsage>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.SenderHash);
        var roomMentionUsageIndexModel = new CreateIndexModel<RoomMentionUsage>(roomMentionUsageIndexKeys, new CreateIndexOptions { Unique = true });
        _roomMentionUsages.Indexes.CreateOne(roomMentionUsageIndexModel);
    }

    public async Task<(bool CanUse, long NextAvailableAt)> TryUseAsync(string roomId, string roomName, string senderHash, string senderName, long currentUnixTimeSeconds)
    {
        var nextAvailableAt = currentUnixTimeSeconds + MentionCooldownSeconds;
        var usage = new RoomMentionUsage
        {
            RoomId = roomId,
            RoomName = roomName,
            SenderHash = senderHash,
            SenderName = senderName,
            LastUsedAt = currentUnixTimeSeconds,
            NextAvailableAt = nextAvailableAt
        };

        try
        {
            await _roomMentionUsages.InsertOneAsync(usage);
            return (true, nextAvailableAt);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Existing usage is checked below so concurrent requests cannot bypass the cooldown.
        }

        var availableUsageFilter = Builders<RoomMentionUsage>.Filter.And(
            Builders<RoomMentionUsage>.Filter.Eq(x => x.RoomId, roomId),
            Builders<RoomMentionUsage>.Filter.Eq(x => x.SenderHash, senderHash),
            Builders<RoomMentionUsage>.Filter.Lte(x => x.NextAvailableAt, currentUnixTimeSeconds)
        );

        var update = Builders<RoomMentionUsage>.Update
            .Set(x => x.RoomName, roomName)
            .Set(x => x.SenderName, senderName)
            .Set(x => x.LastUsedAt, currentUnixTimeSeconds)
            .Set(x => x.NextAvailableAt, nextAvailableAt);

        var updateResult = await _roomMentionUsages.UpdateOneAsync(availableUsageFilter, update);
        if (updateResult.ModifiedCount > 0) return (true, nextAvailableAt);

        var existingUsageFilter = Builders<RoomMentionUsage>.Filter.And(
            Builders<RoomMentionUsage>.Filter.Eq(x => x.RoomId, roomId),
            Builders<RoomMentionUsage>.Filter.Eq(x => x.SenderHash, senderHash)
        );
        var existingUsage = await _roomMentionUsages.Find(existingUsageFilter).FirstOrDefaultAsync();
        return (false, existingUsage?.NextAvailableAt ?? currentUnixTimeSeconds);
    }
}
