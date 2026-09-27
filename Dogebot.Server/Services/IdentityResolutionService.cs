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
/// Every linked identity value is recorded so messages keep resolving after a nickname change.
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

        var knownValueIndexKeys = Builders<IdentityCanonical>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.KnownValues);
        var knownValueIndexModel = new CreateIndexModel<IdentityCanonical>(knownValueIndexKeys);
        _identityCanonicals.Indexes.CreateOne(knownValueIndexModel);
    }

    public async Task<string> ResolveSenderHashAsync(KakaoMessageData data)
    {
        var incomingSenderHash = data.SenderHash;
        var roomId = data.RoomId;
        var senderName = data.SenderName.Trim();

        if (roomId.Length == 0 || senderName.Length == 0 || incomingSenderHash.Length == 0) return incomingSenderHash;

        try
        {
            // Known identity values take priority so a nickname change does not split the identity.
            var knownValueCanonical = await FindCanonicalByKnownValueAsync(roomId, incomingSenderHash);
            if (knownValueCanonical is not null) return await ResolveKnownValueAsync(knownValueCanonical, roomId, senderName, incomingSenderHash);

            var canonical = await FindCanonicalAsync(roomId, senderName);

            if (canonical is null)
            {
                var createdCanonical = await TryCreateCanonicalAsync(roomId, senderName, incomingSenderHash, data.IsLoco);
                if (createdCanonical is not null)
                {
                    // The first observation becomes the canonical identity; a LOCO account id also merges every legacy hash in the room.
                    if (data.IsLoco) await MergeOtherKnownHashesAsync(createdCanonical, roomId, senderName);
                    return incomingSenderHash;
                }

                canonical = await FindCanonicalAsync(roomId, senderName);
                if (canonical is null) return incomingSenderHash;
            }

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
                await MergeOtherKnownHashesAsync(canonical, roomId, senderName);
                return incomingSenderHash;
            }

            // The incoming hash belongs to the same person; merge it into the canonical identity.
            await _userIdentityMergeService.MergeAsync(roomId, incomingSenderHash, canonical.CanonicalValue);
            await AddKnownValueAsync(canonical, incomingSenderHash);
            return canonical.CanonicalValue;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[IDENTITY] Failed to resolve the sender identity. room={RoomId}, sender={SenderName}", roomId, senderName);
            return incomingSenderHash;
        }
    }

    private async Task<string> ResolveKnownValueAsync(IdentityCanonical canonical, string roomId, string senderName, string incomingSenderHash)
    {
        if (canonical.IsAmbiguous) return incomingSenderHash;

        if (incomingSenderHash != canonical.CanonicalValue) await _userIdentityMergeService.MergeAsync(roomId, incomingSenderHash, canonical.CanonicalValue);

        // Follow the latest nickname so future messages can resolve by name as well.
        await UpdateSenderNameAsync(canonical, senderName);

        return canonical.CanonicalValue;
    }

    private async Task<IdentityCanonical?> FindCanonicalAsync(string roomId, string senderName)
    {
        var filter = Builders<IdentityCanonical>.Filter.And(Builders<IdentityCanonical>.Filter.Eq(x => x.RoomId, roomId), Builders<IdentityCanonical>.Filter.Eq(x => x.SenderName, senderName));
        return await _identityCanonicals.Find(filter).FirstOrDefaultAsync();
    }

    private async Task<IdentityCanonical?> FindCanonicalByKnownValueAsync(string roomId, string senderHash)
    {
        var filter = Builders<IdentityCanonical>.Filter.And(Builders<IdentityCanonical>.Filter.Eq(x => x.RoomId, roomId), Builders<IdentityCanonical>.Filter.AnyEq(x => x.KnownValues, senderHash));
        return await _identityCanonicals.Find(filter).SortByDescending(x => x.IsLoco).FirstOrDefaultAsync();
    }

    private async Task<IdentityCanonical?> TryCreateCanonicalAsync(string roomId, string senderName, string canonicalValue, bool isLoco)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var canonical = new IdentityCanonical
        {
            RoomId = roomId,
            SenderName = senderName,
            CanonicalValue = canonicalValue,
            KnownValues = [canonicalValue],
            IsLoco = isLoco,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _identityCanonicals.InsertOneAsync(canonical);
            return canonical;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { return null; }
    }

    private async Task PromoteCanonicalAsync(IdentityCanonical canonical, string locoIdentity)
    {
        var previousValue = canonical.CanonicalValue;

        var update = Builders<IdentityCanonical>.Update
            .Set(x => x.CanonicalValue, locoIdentity)
            .AddToSet(x => x.KnownValues, locoIdentity)
            .Set(x => x.IsLoco, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);

        canonical.CanonicalValue = locoIdentity;
        canonical.IsLoco = true;
        if (!canonical.KnownValues.Contains(locoIdentity)) canonical.KnownValues.Add(locoIdentity);

        _logger.LogInformation("[IDENTITY] Promoted the canonical identity to the LOCO account id. room={RoomId}, sender={SenderName}, previous={PreviousValue}, canonical={CanonicalValue}", canonical.RoomId, canonical.SenderName, previousValue, locoIdentity);
    }

    private async Task UpdateSenderNameAsync(IdentityCanonical canonical, string senderName)
    {
        if (canonical.SenderName == senderName) return;

        var update = Builders<IdentityCanonical>.Update
            .Set(x => x.SenderName, senderName)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        try
        {
            await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);
            canonical.SenderName = senderName;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another person already uses this nickname in the room; keep the recorded name and resolve by value only.
            _logger.LogWarning("[IDENTITY] Failed to track a renamed nickname because another sender already uses it. room={RoomId}, canonical={CanonicalValue}", canonical.RoomId, canonical.CanonicalValue);
        }
    }

    private async Task AddKnownValueAsync(IdentityCanonical canonical, string senderHash)
    {
        if (canonical.KnownValues.Contains(senderHash)) return;

        var update = Builders<IdentityCanonical>.Update
            .AddToSet(x => x.KnownValues, senderHash)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);
        canonical.KnownValues.Add(senderHash);
    }

    private async Task MarkAmbiguousAsync(IdentityCanonical canonical, string incomingSenderHash)
    {
        var update = Builders<IdentityCanonical>.Update
            .Set(x => x.IsAmbiguous, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _identityCanonicals.UpdateOneAsync(Builders<IdentityCanonical>.Filter.Eq(x => x.Id, canonical.Id), update);

        _logger.LogWarning("[IDENTITY] Duplicate sender name detected; automatic merging is disabled. room={RoomId}, sender={SenderName}, canonical={CanonicalValue}, incoming={IncomingSenderHash}", canonical.RoomId, canonical.SenderName, canonical.CanonicalValue, incomingSenderHash);
    }

    private async Task MergeOtherKnownHashesAsync(IdentityCanonical canonical, string roomId, string senderName)
    {
        // Converge every other hash recorded for the same room and sender name, such as hashes from renamed or changed mobile notifications.
        var namePattern = new BsonRegularExpression($"^\\s*{Regex.Escape(senderName)}\\s*$", "i");
        var filter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Regex(x => x.SenderName, namePattern), Builders<ChatStatistics>.Filter.Ne(x => x.SenderHash, canonical.CanonicalValue));

        var recordedHashes = await _chatStatistics.Find(filter).Project(x => x.SenderHash).ToListAsync();

        foreach (var recordedHash in recordedHashes.Where(recordedHash => recordedHash.Length > 0).Distinct(StringComparer.Ordinal))
        {
            await _userIdentityMergeService.MergeAsync(roomId, recordedHash, canonical.CanonicalValue);
            await AddKnownValueAsync(canonical, recordedHash);
        }
    }
}
