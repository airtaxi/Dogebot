using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class ChatStatisticsService : IChatStatisticsService
{
    private readonly IMongoCollection<ChatStatistics> _chatStatistics;
    private readonly IMongoCollection<MessageContent> _messageContents;

    public ChatStatisticsService(IMongoDbService mongoDbService)
    {
        _chatStatistics = mongoDbService.Database.GetCollection<ChatStatistics>("chatStatistics");
        _messageContents = mongoDbService.Database.GetCollection<MessageContent>("messageContents");

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var chatStatsIndexKeys = Builders<ChatStatistics>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.SenderHash);
        var chatStatsIndexModel = new CreateIndexModel<ChatStatistics>(chatStatsIndexKeys);
        _chatStatistics.Indexes.CreateOne(chatStatsIndexModel);

        var messageContentIndexKeys = Builders<MessageContent>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.Content);
        var messageContentIndexModel = new CreateIndexModel<MessageContent>(messageContentIndexKeys);
        _messageContents.Indexes.CreateOne(messageContentIndexModel);
    }

    public async Task RecordMessageAsync(KakaoMessageData data)
    {
        // Filter out blacklisted messages (emoticons, photos, etc.)
        if (MessageBlacklist.IsBlacklisted(data.Content))
            return;

        var chatStatsFilter = Builders<ChatStatistics>.Filter.And(
            Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, data.RoomId),
            Builders<ChatStatistics>.Filter.Eq(x => x.SenderHash, data.SenderHash)
        );

        var chatStatsUpdate = Builders<ChatStatistics>.Update
            .Inc(x => x.MessageCount, 1)
            .Set(x => x.LastMessageTime, data.Time)
            .Set(x => x.SenderName, data.SenderName);

        await _chatStatistics.UpdateOneAsync(
            chatStatsFilter,
            chatStatsUpdate,
            new UpdateOptions { IsUpsert = true }
        );

        var messageContentFilter = Builders<MessageContent>.Filter.And(
            Builders<MessageContent>.Filter.Eq(x => x.RoomId, data.RoomId),
            Builders<MessageContent>.Filter.Eq(x => x.Content, data.Content)
        );

        var messageContentUpdate = Builders<MessageContent>.Update
            .Inc(x => x.Count, 1)
            .Set(x => x.LastTime, data.Time);

        await _messageContents.UpdateOneAsync(
            messageContentFilter,
            messageContentUpdate,
            new UpdateOptions { IsUpsert = true }
        );
    }

    public async Task<List<(string SenderName, long MessageCount)>> GetTopUsersAsync(string roomId, int limit = 10)
    {
        var filter = Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId);
        var sort = Builders<ChatStatistics>.Sort.Descending(x => x.MessageCount);

        var results = await _chatStatistics
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync();

        return results.Select(r => (r.SenderName, r.MessageCount)).ToList();
    }

    public async Task<(int Rank, long MessageCount)?> GetUserRankAsync(string roomId, string senderHash)
    {
        var filter = Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId);
        var sort = Builders<ChatStatistics>.Sort.Descending(x => x.MessageCount);

        var allUsers = await _chatStatistics
            .Find(filter)
            .Sort(sort)
            .ToListAsync();

        var userIndex = allUsers.FindIndex(u => u.SenderHash == senderHash);
        
        if (userIndex == -1)
            return null;

        return (userIndex + 1, allUsers[userIndex].MessageCount);
    }

    public async Task<List<(string Content, long Count)>> GetTopMessagesAsync(string roomId, int limit = 10)
    {
        var filter = Builders<MessageContent>.Filter.Eq(x => x.RoomId, roomId);
        var sort = Builders<MessageContent>.Sort.Descending(x => x.Count);

        var results = await _messageContents
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync();

        return results.Select(r => (r.Content, r.Count)).ToList();
    }

    public async Task<(long TotalMessages, int UniqueUsers)> GetRoomStatisticsAsync(string roomId)
    {
        var filter = Builders<ChatStatistics>.Filter.Eq(x => x.RoomId, roomId);
        
        var users = await _chatStatistics
            .Find(filter)
            .ToListAsync();

        var totalMessages = users.Sum(u => u.MessageCount);
        var uniqueUsers = users.Count;

        return (totalMessages, uniqueUsers);
    }
}
