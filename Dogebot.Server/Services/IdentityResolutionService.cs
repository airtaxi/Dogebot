using Dogebot.Commons;
using Dogebot.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Dogebot.Server.Services;

/// <summary>
/// Resolves the canonical sender identity for incoming messages.
/// A mobile notification hash is registered as a provisional canonical, and the LOCO account id
/// replaces it once the LOCO bridge observes the same sender in the same room.
/// </summary>
public class IdentityResolutionService : IIdentityResolutionService
{
    private readonly IMongoCollection<IdentityCanonical> _identityCanonicals;
    private readonly IMongoCollection<ChatStatistics> _chatStatistics;
    private readonly IUserIdentityMergeService _userIdentityMergeService;
    private readonly ILogger<IdentityResolutionService> _logger;

    public IdentityResolutionService(IMongoDbService mongoDbService, IUserIdentityMergeService userIdentityMergeService, ILogger<IdentityResolutionService> logger)
    {
        _identityCanonicals = mongoDbService.Database.GetCollection<IdentityCanonical>("identityCanonicals");
        _chatStatistics = mongoDbService.Database.GetCollection<ChatStatistics>("chatStatistics");
        _userIdentityMergeService = userIdentityMergeService;
        _logger = logger;
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<IdentityCanonical>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.SenderName);
        var indexModel = new CreateIndexModel<IdentityCanonical>(indexKeys, new CreateIndexOptions { Unique = true });
        _identityCanonicals.Indexes.CreateOne(indexModel);
    }

    public async Task<string> ResolveSenderHashAsync(KakaoMessageData data)
    {
        var incomingSenderHash = data.SenderHash;
        var roomId = data.RoomId;
        var senderName = data.SenderName.Trim();

        if (roomId.Length == 0 || senderName.Length == 0 || incomingSenderHash.Length == 0) return incomingSenderHash;

        try
        {
            var canonical = await FindCanonicalAsync(roomId, senderName);

            if (canonical is null && await TryCreateCanonicalAsync(roomId, senderName, incomingSenderHash, data.IsLoco))
            {
                // The first observation becomes the canonical identity; a LOCO account id also merges every legacy hash in the room.
                if (data.IsLoco) await MergeOtherKnownHashesAsync(roomId, senderName, incomingSenderHash);
                return incomingSenderHash;
            }

            canonical ??= await FindCanonicalAsync(roomId, senderName);
            if (canonical is null) return incomingSenderHash;

            if (canonical.IsAmbiguous) return incomingSenderHash;
            if (incomingSenderHash == canonical.CanonicalValue) return canonical.CanonicalValue;

            if (canonical.IsLoco && data.IsLoco)
            {
                // Two different LOCO account ids for the same room and name; treat it as a duplicate nickname and stop automatic merging.
                await MarkAmbiguousAsync(canonical, incomingSenderHash);
                return incomingSenderHash;
            }

            if (!canonical.IsLoco && data.IsLoco)
            {
                // Promote the provisional mobile hash to the LOCO account id and merge every legacy hash recorded in the room.
                await _userIdentityMergeService.MergeAsync(roomId, canonical.CanonicalValue, incomingSenderHash);
                await PromoteCanonicalAsync(canonical, incomingSenderHash);
                await MergeOtherKnownHashesAsync(roomId, senderName, incomingSenderHash);
                return incomingSenderHash;
            }

            // The incoming hash belongs to the same person; merge it into the canonical identity.
            await _userIdentityMergeService.MergeAsync(roomId, incomingSenderHash, canonical.CanonicalValue);
            return canonical.CanonicalValue;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[IDENTITY] Failed to resolve the sender identity. room={RoomId}, sender={SenderName}", roomId, senderName);
            return incomingSenderHash;
        }
    }

    private async Task<IdentityCanonical?> FindCanonicalAsync(string roomId, string senderName)
    {
        var filter = Builders<IdentityCanonical>.Filter.And(Builders<IdentityCanonical>.Filter.Eq(x => x.RoomId, roomId), Builders<IdentityCanonical>.Filter.Eq(x => x.SenderName, senderName));
        return await _identityCanonicals.Find(filter).FirstOrDefaultAsync();
    }

    private async Task<bool> TryCreateCanonicalAsync(string roomId, string senderName, string canonicalValue, bool isLoco)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var canonical = new IdentityCanonical
        {
            RoomId = roomId,
            SenderName = senderName,
            CanonicalValue = canonicalValue,
            IsLoco = isLoco,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _identityCanonicals.InsertOneAsync(canonical);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }

    private async Task PromoteCanonicalAsync(IdentityCanonical canonical, string locoIdentity)
    {
        var update = Builders<IdentityCanonical>.Update
            .Set(x => x.CanonicalValue, locoIdentity)
            .Set(x => x.IsLoco, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);

        _logger.LogInformation("[IDENTITY] Promoted the canonical identity to the LOCO account id. room={RoomId}, sender={SenderName}, previous={PreviousValue}, canonical={CanonicalValue}", canonical.RoomId, canonical.SenderName, canonical.CanonicalValue, locoIdentity);
    }

    private async Task MarkAmbiguousAsync(IdentityCanonical canonical, string incomingSenderHash)
    {
        var update = Builders<IdentityCanonical>.Update
            .Set(x => x.IsAmbiguous, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);

        _logger.LogWarning("[IDENTITY] Duplicate sender name detected; automatic merging is disabled. room={RoomId}, sender={SenderName}, canonical={CanonicalValue}, incoming={IncomingSenderHash}", canonical.RoomId, canonical.SenderName, canonical.CanonicalValue, incomingSenderHash);
    }

    private async Task MergeOtherKnownHashesAsync(string roomId, string senderName, string canonicalValue)
    {
        // Converge every other hash recorded for the same room and sender name, such as hashes from renamed or changed mobile notifications.
        var namePattern = new BsonRegularExpression($"^\\s*{Regex.Escape(senderName)}\\s*$", "i");
        var filter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Regex(x => x.SenderName, namePattern), Builders<ChatStatistics>.Filter.Ne(x => x.SenderHash, canonicalValue));

        var recordedHashes = await _chatStatistics.Find(filter).Project(x => x.SenderHash).ToListAsync();

        foreach (var recordedHash in recordedHashes.Where(recordedHash => recordedHash.Length > 0).Distinct(StringComparer.Ordinal)) await _userIdentityMergeService.MergeAsync(roomId, recordedHash, canonicalValue);
    }
}
