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
        var contentPatterns = new[]
        {
            "동영상을 보냈습니다\\.",
            "Video",
            "Photo",
            "Emoticon",
            "선착순 선물에 당첨되었어요",
            "사진을 보냈습니다\\.",
            "사진 2장을 보냈습니다\\.",
            "사진 3장을 보냈습니다\\.",
            "사진 4장을 보냈습니다\\.",
            "사진 5장을 보냈습니다\\.",
            "사진 6장을 보냈습니다\\.",
            "사진 7장을 보냈습니다\\.",
            "사진 8장을 보냈습니다\\.",
            "사진 9장을 보냈습니다\\.",
            "사진 11장을 보냈습니다\\.",
            "사진 12장을 보냈습니다\\.",
            "사진 13장을 보냈습니다\\.",
            "사진 14장을 보냈습니다\\.",
            "사진 15장을 보냈습니다\\.",
            "사진 16장을 보냈습니다\\.",
            "사진 17장을 보냈습니다\\.",
            "사진 18장을 보냈습니다\\.",
            "사진 19장을 보냈습니다\\.",
            "사진 20장을 보냈습니다\\.",
            "사진 21장을 보냈습니다\\.",
            "사진 22장을 보냈습니다\\.",
            "사진 23장을 보냈습니다\\.",
            "사진 24장을 보냈습니다\\.",
            "사진 25장을 보냈습니다\\.",
            "사진 26장을 보냈습니다\\.",
            "사진 27장을 보냈습니다\\.",
            "사진 28장을 보냈습니다\\.",
            "사진 29장을 보냈습니다\\.",
            "사진 30장을 보냈습니다\\.",
            "이모티콘을 보냈습니다\\.",
            "^\\(사진\\)",
            "^\\(동영상\\)",
            "^\\(파일\\)",
            "^\\(음성\\)",
            "삭제된 메시지입니다",
            "확률",
            "^\\(링크\\)",
            "^\\(지도\\)",
            "^\\(연락처\\)",
            "^\\(음악\\)",
            "^판사님",
            "^소라고동님",
            "^심심아",
            "^샵검색",
            "^#검색",
            "^\\/",  // Commands starting with /
            "^!"     // Commands starting with !
        };

        foreach (var pattern in contentPatterns)
            filters.Add(filterBuilder.Regex(x => x.Content, new MongoDB.Bson.BsonRegularExpression(pattern, "i")));

        return filterBuilder.Or(filters);
    }
}
