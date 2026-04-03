using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class FortuneService : IFortuneService
{
    private readonly IMongoCollection<DailyFortuneRecord> _dailyFortuneRecords;

    public FortuneService(IMongoDbService mongoDbService)
    {
        _dailyFortuneRecords = mongoDbService.Database.GetCollection<DailyFortuneRecord>("dailyFortuneRecords");
        CreateIndexes();
    }

    public async Task<bool> HasDrawnTodayAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        var filter = Builders<DailyFortuneRecord>.Filter.Eq(record => record.SenderHash, senderHash) &
                     Builders<DailyFortuneRecord>.Filter.Eq(record => record.Date, today);
        return await _dailyFortuneRecords.Find(filter).AnyAsync();
    }

    public async Task RecordDrawAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        var record = new DailyFortuneRecord
        {
            SenderHash = senderHash,
            Date = today
        };
        await _dailyFortuneRecords.InsertOneAsync(record);
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<DailyFortuneRecord>.IndexKeys
            .Ascending(record => record.SenderHash)
            .Ascending(record => record.Date);
        var indexModel = new CreateIndexModel<DailyFortuneRecord>(indexKeys, new CreateIndexOptions { Unique = true });
        _dailyFortuneRecords.Indexes.CreateOne(indexModel);
    }
}
