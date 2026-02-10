using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class ChatStatisticsService : IChatStatisticsService
{
    private readonly IMongoCollection<ChatStatistics> _chatStatistics;
    private readonly IMongoCollection<MessageContent> _messageContents;
    private readonly IMongoCollection<RoomRankingSettings> _roomRankingSettings;
    private readonly IMongoCollection<HourlyChatStatistics> _hourlyChatStatistics;

    public ChatStatisticsService(IMongoDbService mongoDbService)
    {
        _chatStatistics = mongoDbService.Database.GetCollection<ChatStatistics>("chatStatistics");
        _messageContents = mongoDbService.Database.GetCollection<MessageContent>("messageContents");
        _roomRankingSettings = mongoDbService.Database.GetCollection<RoomRankingSettings>("roomRankingSettings");
        _hourlyChatStatistics = mongoDbService.Database.GetCollection<HourlyChatStatistics>("hourlyChatStatistics");

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

        var roomRankingSettingsIndexKeys = Builders<RoomRankingSettings>.IndexKeys.Ascending(x => x.RoomId);
        var roomRankingSettingsIndexModel = new CreateIndexModel<RoomRankingSettings>(roomRankingSettingsIndexKeys, new CreateIndexOptions { Unique = true });
        _roomRankingSettings.Indexes.CreateOne(roomRankingSettingsIndexModel);

        var hourlyStatsIndexKeys = Builders<HourlyChatStatistics>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.SenderHash)
            .Ascending(x => x.DateTime);
        var hourlyStatsIndexModel = new CreateIndexModel<HourlyChatStatistics>(hourlyStatsIndexKeys, new CreateIndexOptions { Unique = true });
        _hourlyChatStatistics.Indexes.CreateOne(hourlyStatsIndexModel);
    }

    public async Task RecordMessageAsync(KakaoMessageData data)
    {
        // Filter out blacklisted messages (emoticons, photos, etc.)
        if (MessageBlacklist.IsBlacklisted(data.Content, data.SenderName))
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

        // Record per-minute statistics (truncated to minute for future granularity)
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(data.Time).UtcDateTime;
        var truncatedToMinute = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, 0, DateTimeKind.Utc);
        var hourlyFilter = Builders<HourlyChatStatistics>.Filter.And(
            Builders<HourlyChatStatistics>.Filter.Eq(x => x.RoomId, data.RoomId),
            Builders<HourlyChatStatistics>.Filter.Eq(x => x.SenderHash, data.SenderHash),
            Builders<HourlyChatStatistics>.Filter.Eq(x => x.DateTime, truncatedToMinute)
        );
        var hourlyUpdate = Builders<HourlyChatStatistics>.Update.Inc(x => x.MessageCount, 1);
        await _hourlyChatStatistics.UpdateOneAsync(
            hourlyFilter,
            hourlyUpdate,
            new UpdateOptions { IsUpsert = true }
        );

        // Only record message content if enabled for this room
        if (await IsMessageContentEnabledAsync(data.RoomId))
        {
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

        return [.. results.Select(r => (r.SenderName, r.MessageCount))];
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

        return [.. results.Select(r => (r.Content, r.Count))];
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

    public async Task<bool> IsMessageContentEnabledAsync(string roomId)
    {
        var filter = Builders<RoomRankingSettings>.Filter.Eq(x => x.RoomId, roomId);
        var settings = await _roomRankingSettings.Find(filter).FirstOrDefaultAsync();
        
        // Default is enabled if no settings exist
        return settings?.IsMessageContentEnabled ?? true;
    }

    public async Task<bool> EnableMessageContentAsync(string roomId, string roomName, string setBy)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var settings = new RoomRankingSettings
        {
            RoomId = roomId,
            RoomName = roomName,
            IsMessageContentEnabled = true,
            SetBy = setBy,
            SetAt = now
        };

        var filter = Builders<RoomRankingSettings>.Filter.Eq(x => x.RoomId, roomId);
        await _roomRankingSettings.ReplaceOneAsync(filter, settings, new ReplaceOptions { IsUpsert = true });

        return true;
    }

    public async Task<bool> DisableMessageContentAsync(string roomId, string roomName, string setBy)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var settings = new RoomRankingSettings
        {
            RoomId = roomId,
            RoomName = roomName,
            IsMessageContentEnabled = false,
            SetBy = setBy,
            SetAt = now
        };

        var filter = Builders<RoomRankingSettings>.Filter.Eq(x => x.RoomId, roomId);
        await _roomRankingSettings.ReplaceOneAsync(filter, settings, new ReplaceOptions { IsUpsert = true });

        // Delete all message content records for this room
        var deleteFilter = Builders<MessageContent>.Filter.Eq(x => x.RoomId, roomId);
        await _messageContents.DeleteManyAsync(deleteFilter);

        return true;
    }

    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

    public async Task<List<(int Hour, long MessageCount)>> GetHourlyStatisticsAsync(string roomId)
    {
        var filter = Builders<HourlyChatStatistics>.Filter.Eq(x => x.RoomId, roomId);

        var results = await _hourlyChatStatistics
            .Find(filter)
            .ToListAsync();

        return [.. results
            .GroupBy(r => new DateTimeOffset(r.DateTime, TimeSpan.Zero).ToOffset(KstOffset).Hour)
            .Select(g => (Hour: g.Key, MessageCount: g.Sum(r => r.MessageCount)))
            .OrderBy(x => x.Hour)];
    }

    public async Task<List<(int Hour, long MessageCount)>> GetUserHourlyStatisticsAsync(string roomId, string senderHash)
    {
        var filter = Builders<HourlyChatStatistics>.Filter.And(
            Builders<HourlyChatStatistics>.Filter.Eq(x => x.RoomId, roomId),
            Builders<HourlyChatStatistics>.Filter.Eq(x => x.SenderHash, senderHash)
        );

        var results = await _hourlyChatStatistics
            .Find(filter)
            .ToListAsync();

        return [.. results
            .GroupBy(r => new DateTimeOffset(r.DateTime, TimeSpan.Zero).ToOffset(KstOffset).Hour)
            .Select(g => (Hour: g.Key, MessageCount: g.Sum(r => r.MessageCount)))
            .OrderBy(x => x.Hour)];
    }
}
