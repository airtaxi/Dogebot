using System.Text.RegularExpressions;
using Dogebot.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

/// <summary>
/// Resolves sender identities for the manual identity link command and merges
/// the room-scoped records of the source identity into the target identity.
/// </summary>
public class UserIdentityLinkService : IUserIdentityLinkService
{
    private readonly IMongoCollection<ChatStatistics> _chatStatistics;
    private readonly IMongoCollection<RoomMigrationMapping> _migrationMappings;
    private readonly IUserIdentityMergeService _userIdentityMergeService;
    private readonly ILogger<UserIdentityLinkService> _logger;

    public UserIdentityLinkService(IMongoDbService mongoDbService, IUserIdentityMergeService userIdentityMergeService, ILogger<UserIdentityLinkService> logger)
    {
        _chatStatistics = mongoDbService.Database.GetCollection<ChatStatistics>("chatStatistics");
        _migrationMappings = mongoDbService.Database.GetCollection<RoomMigrationMapping>("roomMigrationMappings");
        _userIdentityMergeService = userIdentityMergeService;
        _logger = logger;
    }

    public async Task<SenderIdentityResolution> ResolveIdentityAsync(string roomId, string identityArgument)
    {
        var argument = identityArgument.Trim();
        if (argument.Length == 0) return SenderIdentityResolution.NotFound();

        // A sender hash is the identity itself, so an exact hash match takes priority over the nickname match.
        var hashFilter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Eq(x => x.SenderHash, argument));
        var hashMatch = await _chatStatistics.Find(hashFilter).SortByDescending(x => x.LastMessageTime).FirstOrDefaultAsync();
        if (hashMatch is not null) return SenderIdentityResolution.Resolved(CreateReference(hashMatch));

        // Fall back to a nickname match; multiple distinct hashes for one nickname are ambiguous.
        var namePattern = new BsonRegularExpression($"^\\s*{Regex.Escape(argument)}\\s*$", "i");
        var nameFilter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Regex(x => x.SenderName, namePattern));
        var nameMatches = await _chatStatistics.Find(nameFilter).SortByDescending(x => x.LastMessageTime).ToListAsync();

        var distinctHashes = nameMatches.Select(x => x.SenderHash).Where(senderHash => senderHash.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (distinctHashes.Count == 0) return SenderIdentityResolution.NotFound();

        var candidates = distinctHashes.Select(senderHash => CreateReference(nameMatches.First(x => x.SenderHash == senderHash))).ToList();
        return candidates.Count == 1 ? SenderIdentityResolution.Resolved(candidates[0]) : SenderIdentityResolution.Ambiguous(candidates);
    }

    public async Task LinkIdentitiesAsync(string roomId, string sourceSenderHash, string targetSenderHash)
    {
        if (sourceSenderHash == targetSenderHash) return;

        await _userIdentityMergeService.MergeRoomScopedAsync(roomId, sourceSenderHash, targetSenderHash);

        // A pending lazy migration mapping for the merged hash would only merge data that no longer exists.
        var mappingFilter = Builders<RoomMigrationMapping>.Filter.And(Builders<RoomMigrationMapping>.Filter.Eq(x => x.TargetRoomId, roomId), Builders<RoomMigrationMapping>.Filter.Eq(x => x.OldSenderHash, sourceSenderHash));
        await _migrationMappings.DeleteManyAsync(mappingFilter);

        _logger.LogWarning("[IDENTITY_LINK] Linked sender identities in room {RoomId}. source={SourceSenderHash}, target={TargetSenderHash}", roomId, sourceSenderHash, targetSenderHash);
    }

    private static SenderIdentityReference CreateReference(ChatStatistics statistics) => new(statistics.SenderHash, statistics.SenderName.Trim(), statistics.MessageCount, statistics.LastMessageTime);
}
