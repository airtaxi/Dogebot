using Dogebot.Commons;
using Dogebot.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Dogebot.Server.Services;

/// <summary>
/// Resolves the sender hash before a message is recorded.
/// A completely new hash in the room is migrated to the most recently active document
/// of the same sender name, so switching clients keeps the same identity.
/// </summary>
public class SenderHashResolutionService : ISenderHashResolutionService
{
    private readonly IMongoCollection<ChatStatistics> _chatStatistics;
    private readonly ILogger<SenderHashResolutionService> _logger;

    public SenderHashResolutionService(IMongoDbService mongoDbService, ILogger<SenderHashResolutionService> logger)
    {
        _chatStatistics = mongoDbService.Database.GetCollection<ChatStatistics>("chatStatistics");
        _logger = logger;
    }

    public async Task<string> ResolveSenderHashAsync(KakaoMessageData data)
    {
        var roomId = data.RoomId;
        var incomingSenderHash = data.SenderHash;
        var senderName = data.SenderName.Trim();

        if (roomId.Length == 0 || senderName.Length == 0 || incomingSenderHash.Length == 0) return incomingSenderHash;

        try
        {
            // A hash that already has a document in the room keeps its own identity.
            var knownHashFilter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Eq(x => x.SenderHash, incomingSenderHash));
            if (await _chatStatistics.Find(knownHashFilter).Limit(1).AnyAsync()) return incomingSenderHash;

            // A completely new hash is migrated to the most recently active document of the same name.
            var namePattern = new BsonRegularExpression($"^\\s*{Regex.Escape(senderName)}\\s*$", "i");
            var sameNameFilter = Builders<ChatStatistics>.Filter.And(Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId), Builders<ChatStatistics>.Filter.Regex(x => x.SenderName, namePattern));
            var sameNameDocument = await _chatStatistics.Find(sameNameFilter).SortByDescending(x => x.LastMessageTime).FirstOrDefaultAsync();
            if (sameNameDocument is null) return incomingSenderHash;

            _logger.LogInformation("[SENDER_HASH] Migrated a new sender hash to the same-name document. room={RoomId}, sender={SenderName}, incoming={IncomingSenderHash}, migrated={MigratedSenderHash}", roomId, senderName, incomingSenderHash, sameNameDocument.SenderHash);
            return sameNameDocument.SenderHash;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[SENDER_HASH] Failed to resolve the sender hash. room={RoomId}, sender={SenderName}", roomId, senderName);
            return incomingSenderHash;
        }
    }
}
