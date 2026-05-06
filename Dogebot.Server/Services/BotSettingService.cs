using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class BotSettingService : IBotSettingService
{
    private const string MessageDeliveryModeKey = "messageDeliveryMode";

    private readonly IMongoCollection<BotSetting> _settings;

    public BotSettingService(IMongoDbService mongoDbService)
    {
        _settings = mongoDbService.Database.GetCollection<BotSetting>("botSettings");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<BotSetting>.IndexKeys.Ascending(setting => setting.Key);
        var indexModel = new CreateIndexModel<BotSetting>(indexKeys, new CreateIndexOptions { Unique = true });
        _settings.Indexes.CreateOne(indexModel);
    }

    public async Task<MessageDeliveryMode> GetMessageDeliveryModeAsync()
    {
        var filter = Builders<BotSetting>.Filter.Eq(setting => setting.Key, MessageDeliveryModeKey);
        var setting = await _settings.Find(filter).FirstOrDefaultAsync();
        if (setting is null) return MessageDeliveryMode.Single;

        return Enum.TryParse<MessageDeliveryMode>(setting.Value, true, out var messageDeliveryMode)
            ? messageDeliveryMode
            : MessageDeliveryMode.Single;
    }

    public async Task SetMessageDeliveryModeAsync(MessageDeliveryMode messageDeliveryMode, string updatedBy)
    {
        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<BotSetting>.Filter.Eq(setting => setting.Key, MessageDeliveryModeKey);
        var update = Builders<BotSetting>.Update
            .Set(setting => setting.Key, MessageDeliveryModeKey)
            .Set(setting => setting.Value, messageDeliveryMode.ToString())
            .Set(setting => setting.UpdatedBy, updatedBy)
            .Set(setting => setting.UpdatedAt, currentUnixTime);

        await _settings.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }
}
