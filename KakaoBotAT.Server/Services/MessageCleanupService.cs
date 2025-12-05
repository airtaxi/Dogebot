using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

/// <summary>
/// Service for cleaning up blacklisted messages from the database.
/// This is useful for removing historical data that should not have been recorded.
/// </summary>
public class MessageCleanupService
{
    private readonly IMongoCollection<MessageContent> _messageContents;

    public MessageCleanupService(IMongoDbService mongoDbService)
    {
        _messageContents = mongoDbService.Database.GetCollection<MessageContent>("messageContents");
    }

    /// <summary>
    /// Counts how many blacklisted messages exist in the database.
    /// </summary>
    public async Task<long> CountBlacklistedMessagesAsync()
    {
        var filter = BuildBlacklistFilter();
        return await _messageContents.CountDocumentsAsync(filter);
    }

    /// <summary>
    /// Retrieves a sample of blacklisted messages for preview.
    /// </summary>
    public async Task<List<MessageContent>> PreviewBlacklistedMessagesAsync(int limit = 10)
    {
        var filter = BuildBlacklistFilter();
        return await _messageContents.Find(filter).Limit(limit).ToListAsync();
    }

    /// <summary>
    /// Deletes all blacklisted messages from the database.
    /// Returns the number of deleted documents.
    /// </summary>
    public async Task<long> DeleteBlacklistedMessagesAsync()
    {
        var filter = BuildBlacklistFilter();
        var result = await _messageContents.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    /// <summary>
    /// Deletes blacklisted messages for a specific room.
    /// Returns the number of deleted documents.
    /// </summary>
    public async Task<long> DeleteBlacklistedMessagesForRoomAsync(string roomId)
    {
        var blacklistFilter = BuildBlacklistFilter();
        var roomFilter = Builders<MessageContent>.Filter.Eq(x => x.RoomId, roomId);
        var combinedFilter = Builders<MessageContent>.Filter.And(roomFilter, blacklistFilter);
        
        var result = await _messageContents.DeleteManyAsync(combinedFilter);
        return result.DeletedCount;
    }

    private FilterDefinition<MessageContent> BuildBlacklistFilter()
    {
        var filterBuilder = Builders<MessageContent>.Filter;
        var filters = new List<FilterDefinition<MessageContent>>();

        // Add regex filters for each blacklist pattern
        var patterns = new[]
        {
            "이모티콘을 보냈습니다\\.",
            "^\\(사진\\)",
            "^\\(동영상\\)",
            "^\\(파일\\)",
            "^\\(음성\\)",
            "삭제된 메시지입니다",
            "^\\(링크\\)",
            "^\\(지도\\)",
            "^\\(연락처\\)",
            "^\\(음악\\)",
            "^샵검색",
            "^#검색",
            "^\\/",  // Commands starting with /
            "^!"     // Commands starting with !
        };

        foreach (var pattern in patterns)
        {
            filters.Add(filterBuilder.Regex(x => x.Content, new MongoDB.Bson.BsonRegularExpression(pattern, "i")));
        }

        return filterBuilder.Or(filters);
    }
}
